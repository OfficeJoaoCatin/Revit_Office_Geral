# Análise: travamento em `Importar Detalhes`

## Objetivo

Este documento registra o comportamento atual do comando **Importar Detalhes**, os riscos de travamento observados e recomendações para estabilizar a rotina antes de ampliá-la.

O problema relatado é grave: durante a criação dos detalhes o Revit deixa de responder, não cria as vistas esperadas e os usuários precisam encerrá-lo à força.

## Fluxo atual

1. A ferramenta lê o DWG internamente, detecta os limites dos detalhes e mostra os previews.
2. Para cada detalhe selecionado, ela tenta gerar um DWG temporário de alta fidelidade usando GstarCAD.
3. O GstarCAD abre o DWG original em modo de leitura, seleciona as entidades dentro do limite do detalhe e executa `WBlock` para gravar o recorte.
4. O motor interno ACadSharp é usado quando selecionado ou quando o usuário aceita explicitamente continuar com ele. Se a alta fidelidade estiver selecionada e o GstarCAD falhar, o detalhe é informado como falha, sem troca automática de motor.
5. O Revit cria uma Legend ou Drafting View, importa o DWG temporário e tenta reposicionar a importação.

O GstarCAD existe para preservar com maior fidelidade blocos, atributos, estilos de texto, layers, linetypes, hachuras e demais propriedades gráficas que podem sofrer diferenças no recorte feito pelo motor interno.

Referências: [GstarCadDetailExporter.cs](./Import/DetailImport/GstarCadDetailExporter.cs:38), [DetailDwgWriter.cs](./Import/DetailImport/DetailDwgWriter.cs:26) e [SheetImporter.cs](./Import/DetailImport/SheetImporter.cs:28).

## Versão do GstarCAD chamada

O código procura os seguintes ProgIDs COM, nesta ordem:

1. `GStarCAD.Application`
2. `Gcad.Application`
3. `GStarCAD.Application.23`
4. `Gcad.Application.23`
5. `GStarCAD.Application.22`
6. `Gcad.Application.22`
7. `GcadVba.GcadApplication`

Na máquina analisada, os ProgIDs genéricos e os ProgIDs `.23` estão registrados para:

```text
C:\Program Files\Gstarsoft\GstarCAD2023\gcad.exe /Automation
```

Os ProgIDs `.22` apontam para `GstarCAD2022`. Como os genéricos são tentados antes, o comportamento atual é abrir **GstarCAD 2023** quando ele estiver instalado e registrado normalmente.

Referência: [GstarCadDetailExporter.cs](./Import/DetailImport/GstarCadDetailExporter.cs:14).

## Quando o GstarCAD já está aberto

O código não verifica se existe uma sessão do GstarCAD em uso e não tenta anexar-se explicitamente a ela. Ele pede um novo objeto COM por `Activator.CreateInstance`, abre o DWG de origem e, ao fim da rotina, chama `Quit()` no objeto de aplicação utilizado.

Na prática, o resultado depende de como a versão instalada do GstarCAD atende a automação COM:

- Em condição normal, deve iniciar uma sessão de automação própria, oculta, para o comando.
- Se o servidor COM reutilizar a sessão que o usuário já abriu, o comando pode afetar essa sessão: define `Visible = false`, desabilita alertas, abre o DWG e chama `Quit()` ao terminar.
- Se existirem documentos alterados, diálogo de licença, recuperação de arquivo, fontes ausentes, referências externas ou qualquer mensagem pendente no GstarCAD, a automação pode aguardar uma resposta. Como a sessão é configurada como invisível, o usuário pode não conseguir enxergar nem responder à janela.

Esse comportamento é um risco tanto para travamento quanto para interferência em uma sessão de CAD que o usuário já estava utilizando.

Referências: [GstarCadDetailExporter.cs](./Import/DetailImport/GstarCadDetailExporter.cs:121), [GstarCadDetailExporter.cs](./Import/DetailImport/GstarCadDetailExporter.cs:175) e [GstarCadDetailExporter.cs](./Import/DetailImport/GstarCadDetailExporter.cs:271).

## Possíveis causas do travamento

### 1. Automação COM bloqueada pelo GstarCAD

O GstarCAD é acionado de forma síncrona, com chamadas como abertura do DWG, criação do conjunto de seleção e `WBlock`. Não há timeout nem mecanismo de cancelamento para essas chamadas. Se qualquer uma delas ficar aguardando uma janela interna do CAD ou uma operação pesada, o comando não retorna ao Revit.

O trecho mais suspeito é o `WBlock`, executado uma vez para cada detalhe selecionado. Um recorte grande pode capturar muitas entidades, blocos, Xrefs ou elementos com geometria extensa.

Referências: [GstarCadDetailExporter.cs](./Import/DetailImport/GstarCadDetailExporter.cs:58), [GstarCadDetailExporter.cs](./Import/DetailImport/GstarCadDetailExporter.cs:79) e [GstarCadDetailExporter.cs](./Import/DetailImport/GstarCadDetailExporter.cs:193).

### 2. GstarCAD oculto aguardando diálogo

Após criar o objeto COM, o comando força `Visible = false` e `DisplayAlerts = false`. Nem todo alerta de CAD é suprimido por `DisplayAlerts`; diálogos de licença, inicialização, recuperação, proxy object, Xref, fonte SHX/TTF ou arquivo já aberto podem continuar bloqueando o processo.

Como essa operação acontece na thread que executa o comando do Revit, o usuário vê apenas o Revit sem resposta.

Referência: [GstarCadDetailExporter.cs](./Import/DetailImport/GstarCadDetailExporter.cs:180).

### 3. Uma única transação longa no Revit

Toda a criação dos detalhes ocorre dentro de uma única `Transaction`: geração do DWG temporário, criação/duplicação de vista, importação, regeneração e reposicionamento para todos os itens escolhidos. Uma importação CAD pesada ou inválida pode tornar a transação excessivamente longa e manter a interface bloqueada.

Referência: [SheetImporter.cs](./Import/DetailImport/SheetImporter.cs:53).

### 4. Regenerações e importações repetidas

Para cada detalhe, o Revit importa um DWG com `VisibleLayersOnly = false`, depois chama `Regenerate()` para obter a caixa envolvente e reposicionar o item. Em arquivos com muitas layers, hachuras, blocos ou entidades pesadas, isso pode multiplicar o tempo de processamento e o consumo de memória.

Referências: [SheetImporter.cs](./Import/DetailImport/SheetImporter.cs:137) e [SheetImporter.cs](./Import/DetailImport/SheetImporter.cs:163).

### 5. Seleção cruzada maior que o detalhe esperado

O GstarCAD usa seleção cruzada baseada no bounding box do limite do detalhe, com uma tolerância adicional. Se o limite estiver grande, deslocado ou sobreposto a outros detalhes, o `WBlock` pode exportar muito mais conteúdo que o esperado.

Referência: [GstarCadDetailExporter.cs](./Import/DetailImport/GstarCadDetailExporter.cs:73).

### 6. Encerramento da sessão COM

No fim da operação, a rotina fecha o documento CAD e chama `Quit()` no GstarCAD. Se a automação ainda estiver processando, se existir uma mensagem interna ou se a sessão não for exclusiva do comando, esse encerramento pode ficar bloqueado ou interferir com o trabalho do usuário.

Referência: [GstarCadDetailExporter.cs](./Import/DetailImport/GstarCadDetailExporter.cs:151).

### 7. Diagnóstico insuficiente após encerramento forçado

Os eventos atualmente são enviados apenas para `Trace.WriteLine`; não existe log persistente por execução com etapa, horário, detalhe atual e duração. Quando o usuário encerra o Revit à força, não há uma trilha confiável para dizer se a espera ocorreu no GstarCAD, no `WBlock`, no `Document.Import`, na regeneração ou no encerramento do COM.

Referência: [TraceLogger.cs](./Services/TraceLogger.cs:17).

## Diagnóstico mais provável

O candidato principal é uma chamada ao GstarCAD, especialmente abertura de arquivo ou `WBlock`, aguardando um diálogo invisível ou processando um recorte muito pesado. O segundo candidato é a importação de um DWG temporário complexo no Revit, seguida de regeneração, dentro da transação única.

Sem um log persistente da última etapa concluída, não é possível afirmar qual dos dois é o responsável em cada ocorrência.

## Recomendações de correção

### Prioridade 1: tornar o GstarCAD opcional e seguro

- Adicionar uma opção explícita: `Alta fidelidade (GstarCAD)` ou `Modo interno (mais estável)`.
- Para a rotina padrão, considerar iniciar no modo interno e reservar GstarCAD para arquivos que realmente precisem de preservação gráfica avançada.
- Antes de iniciar a automação, detectar se há GstarCAD aberto. Se houver, avisar o usuário e impedir o uso da sessão existente, ou pedir que ele feche o CAD antes de continuar.
- Não ocultar uma sessão que não tenha sido comprovadamente criada pelo próprio plugin.
- Registrar a versão/ProgID escolhida, PID quando disponível, arquivo aberto e hora de início/fim.

### Prioridade 2: isolar a automação CAD

- Executar o recorte do GstarCAD em um processo auxiliar próprio, não dentro da execução do comando do Revit. O Revit deve continuar responsável apenas pela criação/importação das vistas.
- Aplicar watchdog e timeout no processo auxiliar. Se ele travar, encerrar somente o processo auxiliar e continuar pelo motor interno ou reportar o detalhe como falho.
- Garantir que somente a sessão iniciada pelo plugin seja encerrada; nunca chamar `Quit()` sobre uma sessão do usuário.

Observação: chamadas à API do Revit não podem migrar para thread de fundo. Apenas o trabalho CAD externo deve ser isolado; `Document.Import`, transações e criação de vistas continuam no contexto controlado do Revit.

### Prioridade 3: reduzir o tempo bloqueado no Revit

- Criar uma transação por detalhe, ou usar `TransactionGroup` com transações curtas. Assim, cada item concluído é salvo, falhas não prendem toda a operação e é possível informar progresso real.
- Atualizar uma janela de progresso entre itens, mostrando `Detalhe atual`, `etapa atual` e tempo decorrido.
- Evitar `Regenerate()` quando não for necessário; medir o custo antes/depois de cada importação.
- Validar o tamanho do recorte e a quantidade de entidades antes do `WBlock`/importação. Recortes acima de um limite configurável devem pedir confirmação ou falhar de forma controlada.
- Considerar importar apenas layers necessárias ou remover layers irrelevantes do DWG temporário antes de chamar o Revit.

### Prioridade 4: observabilidade e recuperação

- Criar log persistente em `%LOCALAPPDATA%\RevitFamilyImporter\Logs`, um arquivo por execução.
- Registrar as etapas: leitura do DWG, detecção, início/fim de GstarCAD, abertura do DWG, seleção, `WBlock`, criação de vista, importação, regeneração, reposicionamento e encerramento.
- Gravar duração por detalhe, quantidade de entidades selecionadas, caminho do DWG temporário, versão do GstarCAD e exceções.
- Preservar os DWGs temporários quando uma etapa falhar, para permitir inspeção técnica posterior.

### Prioridade 5: pré-validação do DWG

- Avisar antecipadamente sobre Xrefs, proxy objects, imagens externas, fontes não encontradas, recortes excessivamente grandes e detalhes sem entidades.
- Oferecer uma importação de teste de um único detalhe antes de processar todos.
- Limitar o lote inicial ou processar em blocos para reduzir pressão de memória.

## Plano de teste recomendado

1. Importar um único detalhe simples, com GstarCAD fechado.
2. Repetir com GstarCAD 2023 aberto e outro DWG alterado, verificando se a sessão do usuário permanece visível e aberta.
3. Repetir com o DWG fonte já aberto no GstarCAD.
4. Testar um detalhe com blocos, atributos, hachuras, fontes especiais e Xrefs.
5. Testar o mesmo arquivo sem GstarCAD, forçando o fallback interno.
6. Importar um lote pequeno e um lote grande, medindo tempo por detalhe e uso de memória.
7. Simular falha do GstarCAD durante `WBlock` e confirmar que o Revit volta a responder e registra claramente o item que falhou.

## Critérios de aceite para a correção

- Nenhuma espera externa pode bloquear o Revit indefinidamente.
- O usuário nunca perde ou tem ocultada uma sessão própria do GstarCAD.
- Cada detalhe deve informar progresso e pode falhar isoladamente.
- Itens concluídos devem permanecer criados mesmo que outro item falhe.
- O relatório final deve distinguir: alta fidelidade GstarCAD, fallback interno, falha CAD, falha de importação Revit e falha de criação de vista.
- Após uma falha, deve existir log e DWG temporário suficientes para reproduzir o problema.

## Correções implementadas

As seguintes medidas foram aplicadas no Office-Geral:

- O motor interno passou a ser o modo padrão; o uso do GstarCAD exige a opção explícita `Usar GstarCAD para alta fidelidade`.
- Se houver um processo GstarCAD/Gcad já aberto, o usuário pode cancelar a importação ou autorizar o motor interno apenas para a execução atual. A opção de alta fidelidade permanece selecionada para tentativas posteriores.
- Quando a alta fidelidade é habilitada, a sessão iniciada pelo comando fica visível e com alertas ativos. Assim, diálogos de licença, recuperação, fontes ou arquivos não ficam escondidos atrás do Revit.
- O número máximo de entidades por detalhe é configurável e inicia em `10000`. Acima desse limite, o detalhe é recusado antes da importação. A seleção real feita no GstarCAD também é validada antes do `WBlock`.
- A criação foi dividida em uma transação por detalhe. Falhas individuais são revertidas somente no item afetado; as vistas já concluídas permanecem no projeto.
- Foram adicionados registros persistentes em `%LOCALAPPDATA%\RevitFamilyImporter\Logs`, com etapa, horário, detalhe e duração. Os DWGs temporários não são apagados pela rotina, permitindo inspeção após uma falha.

## Melhoria ainda recomendada

O isolamento do GstarCAD em um processo auxiliar com watchdog continua sendo a evolução indicada para obter timeout real. Não é seguro interromper arbitrariamente uma chamada COM do GstarCAD dentro do processo do Revit; por isso a versão atual reduz o risco por padrão, mas não tenta matar uma chamada CAD que já tenha iniciado.
