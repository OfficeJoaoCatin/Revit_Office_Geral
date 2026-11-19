> Nota: as seções anteriores a "Somente conflitos físicos confirmados" descrevem etapas anteriores e o comportamento aproximado histórico. A política atual é a seção final abaixo.

# Desempenho das análises de clash

## Abordagem

- O Clash 2D usa o filtro nativo de categorias na coleta da vista e reutiliza a projeção. As regiões do DWG são indexadas por caixas 2D; regiões ou consultas muito extensas usam uma busca de fallback para não excluir candidatos. A verificação geométrica existente continua sendo aplicada aos candidatos e a ordem original dos resultados é preservada.
- A chave usada para lembrar marcações do DWG é calculada apenas para regiões que efetivamente geram conflito.
- Conflitos em Links guarda IDs dos elementos do link e mantém uma cache limitada a 1.024 elementos hospedeiros, reutilizando sólidos e caixas quando o mesmo elemento aparece em vários candidatos. O teste geométrico e as verificações aproximadas continuam em vigor.
- As rotinas exibem uma janela de progresso independente e permitem solicitar cancelamento. A janela não acessa a API do Revit: a coleta e a geometria continuam sendo processadas no contexto permitido do Revit. A solicitação é observada entre etapas e iterações; durante a montagem final da tabela, o cancelamento fica indisponível. Uma chamada individual do Revit que esteja bloqueada ou uma operação booleana em andamento não pode ser interrompida imediatamente. O cancelamento não apresenta resultados parciais nem altera o modelo.
- A leitura das categorias do link e das layers do DWG também exibe progresso. Elas podem demorar na primeira consulta de um modelo ou de uma vista.

## Verificação sugerida em projetos reais

1. Em cópias do mesmo projeto e das mesmas versões dos vínculos/DWGs, anote as categorias, vista, layer, tolerância e modo. Execute as versões anterior e nova com configurações iguais e compare o conjunto de pares de IDs, a quantidade de avisos e os casos aproximados; não compare apenas a ordem ou o número da linha.
2. Repita cada cenário ao menos três vezes e registre duração e pico de memória, separando a primeira execução das seguintes. A primeira coleta de uma vista pode exigir reconstrução de geometria do Revit.
3. Inclua uma planta pequena, uma planta extensa, um DWG com muitas entidades e um link complexo com categorias aninhadas. Teste linhas muito longas ou regiões que cubram quase toda a vista, além de elementos muito grandes e diferentes tolerâncias.
4. Durante a leitura do DWG, coleta de categorias e comparação, clique em **Cancelar análise**. A janela deve informar o pedido e terminar assim que a operação atual do Revit retornar; não deve abrir uma lista parcial. Repita se o Revit estiver ocupado com uma única geometria complexa.
5. Confirme que as marcações concluídas são restauradas como antes e que seleção, enquadramento e exportação continuam funcionando. Faça testes também com modelos cujos elementos de um mesmo hospedeiro sejam candidatos para várias partes de um link.

O teste automatizado `dotnet run --project tests\DwgClashChecks\DwgClashChecks.csproj -c Release` compara o índice com a busca original em cenários sintéticos e verifica fallback e cancelamento. Em uma grade sintética de 10.000 regiões separadas, um elemento consultou uma região candidata; isso não é estimativa de tempo ou ganho para projetos reais.

## Fontes

- [Autodesk: FilteredElementCollector e filtros nativos](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/263cf06b-98be-6f91-c4da-fb47d01688f3.htm)
- [Autodesk: coleta de elementos em uma vista e custo inicial](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/6359776d-915e-f8a2-4147-b31024671ee1.htm)
- [Autodesk: External Events para diálogos não modais](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API/files/Revit_API_Developers_Guide/Advanced_Topics/Revit_API_Revit_API_Developers_Guide_Advanced_Topics_External_Events_html.html)
- [Autodesk: Idling e impacto de operações longas na interface](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/56145d84-e948-730a-dc72-2a7b88a50a99.htm)

## Nova triagem e medições em links

Antes de extrair a geometria detalhada, o comando usa a caixa do elemento do link, transformada para o hospedeiro, com um filtro nativo de caixas. A instância do link é excluída da consulta. Se não houver nenhum candidato, o elemento é dispensado; quando a caixa estiver indisponível ou a consulta falhar, segue pela análise completa. Os demais elementos continuam usando a busca detalhada anterior. Em modelos densos, a consulta adicional pode ter custo sem eliminar muitos elementos; avalie as medições e compare os resultados com modelos reais.

A leitura de sólidos e a alternativa por caixas reutilizam a primeira geometria obtida quando possível. As caixas de sólidos são convertidas ao sistema do modelo antes da comparação, corrigindo falsos descartes em caixas transformadas. Isso pode aumentar os conflitos precisos em comparação à versão anterior; compare os pares e tipos de aviso, inclusive itens antes aproximados.

A janela de resultados mostra o tempo total de análise, pares e elementos dispensados pela triagem. Passe o mouse sobre essa linha para ver os tempos de busca, geometria e comparação; esses tempos não incluem a montagem final da janela nem, necessariamente, todas as outras etapas. Compare execuções nas mesmas condições e anote tanto os tempos quanto os resultados. `dotnet run --project tests/LinkConflictGeometryChecks/LinkConflictGeometryChecks.csproj -c Release` verifica transformações de caixas com substitutos independentes da API; não substitui um teste em modelos reais no Revit.

## Reutilização de candidatos do hospedeiro

A triagem inicial recolhe até 256 IDs. Quando a caixa detalhada está contida na caixa inicial, reaplica o mesmo filtro nativo sobre esses IDs, evitando percorrer novamente o documento inteiro. Se o limite for excedido, as caixas não forem contidas ou houver qualquer falha, usa a busca completa anterior. A ordem da coleta inicial é mantida. A vantagem depende do modelo: a triagem inicial pode ter custo adicional em áreas densas, então a comparação de tempos e de resultados no Revit continua necessária.

## Modelos densos e andamento

Para até 16 partes geométricas com caixas completas, o filtro nativo pesquisa candidatos que cruzem pelo menos uma das caixas; quando houver caixas ausentes, incompletas ou em excesso, usa a caixa combinada. Os sólidos e as caixas aproximadas dos candidatos continuam sendo verificados após essa triagem. O efeito depende da geometria: elementos compostos por uma única parte não ganham com esse filtro.

O nome, família e tipo dos hospedeiros e a identificação de famílias superiores são reaproveitados durante a análise com memória limitada. Quando não há marcações salvas, a abertura da tabela evita calcular hashes para cada conflito. A janela de progresso exibe contagem de avisos aproximados e tempos acumulados de busca, geometria e comparação; uma chamada individual do Revit em andamento não é contabilizada até retornar. Os tempos são para diagnóstico, não prova de aceleração.

Teste com os mesmos modelos e categorias, incluindo famílias aninhadas, elementos com várias partes, geometrias sem sólidos, vínculos rotacionados e projetos densos. Compare pares, volumes, pontos e tipo dos avisos; meça tempo e memória. Em especial, a busca inicial de até 256 candidatos pode ser mais custosa em certas regiões e precisa ser medida no Revit.

## Redução de avisos aproximados

Quando ambos os elementos têm sólidos e a extração e as operações booleanas terminam sem falha, uma sobreposição apenas das caixas não gera mais aviso. Uma colisão volumétrica continua sendo relatada. Se faltarem sólidos, alguma malha ou parte de geometria não puder ser examinada como sólido, a extração falhar ou uma booleana falhar, a sobreposição das caixas continua gerando aviso aproximado. Um par sem aviso permanece elegível para comparação com outras partes de famílias aninhadas.

O progresso separa avisos aproximados por ausência de sólidos, geometria parcial e falha booleana, e informa extrações de geometria do hospedeiro em relação às solicitações à cache. Contadores são cumulativos, e suas razões são exclusivas seguindo essa ordem. A taxa de extração permite investigar se o limite de memória da cache está causando trabalho repetido, sem aumentar arbitrariamente o consumo de memória.

Compare em modelo real os conflitos por par e volume, verificando casos com sólidos sem interseção, colisões confirmadas, malhas, geometria parcial, booleanas que falham, objetos com pequenas folgas, vínculos rotacionados e famílias aninhadas. A redução de avisos não elimina a necessidade de revisar os aproximados remanescentes. Meça tempo e memória; essa alteração reduz a geração de falsos positivos, mas não elimina as extrações ou todas as comparações geométricas.

## Diagnóstico rápido em links

Na janela de configuração, marque **Diagnóstico rápido** para percorrer uma amostra de até 200 elementos a partir da posição informada na lista coletada, 20.000 pares ou 60 segundos. Os limites de pares e tempo são conferidos entre operações; uma única chamada da API do Revit pode demorar mais que o limite. A coleta inicial também pode ultrapassar o tempo porque uma chamada do Revit em andamento não é interrompida. O último elemento pode estar incompleto e a amostra não representa necessariamente o modelo inteiro e a ordem pode mudar entre execuções.

O diagnóstico não monta a tabela de conflitos e não altera o projeto. O resumo copiável mostra os contadores de pares sem sólidos no link, no hospedeiro ou em ambos, até dez combinações de categoria e vinte pares de exemplo. Contabiliza solicitações de geometria, hospedeiros distintos, extrações e extrações repetidas do mesmo hospedeiro, para diferenciar cache insuficiente de elementos distintos. Os exemplos e dados ficam na janela; o usuário decide se deseja copiá-los.

O diagnóstico é desmarcado por padrão. Execute-o com as mesmas categorias da análise lenta, copie o resumo e utilize-o para decidir a próxima alteração. Faça também um teste pequeno conhecido de análise normal, conferindo conflitos e o funcionamento de cancelamento e seleção após fechar o diagnóstico.

## DWG no hospedeiro e posição de diagnóstico

O Conflitos em Links ignora instâncias de importação do projeto hospedeiro identificadas como DWG pelo nome da categoria, instância ou tipo. Um filtro nativo é aplicado quando as categorias são identificadas e aceitas pelo Revit; a verificação por elemento continua como alternativa. Outros tipos de elementos e outros formatos de importação não são excluídos por essa regra. Se o Revit não expuser nenhum nome identificável com extensão .dwg, a instância permanece na análise, para não descartar conteúdo por engano.

O diagnóstico rápido permite indicar uma posição inicial em base 1. O valor 1 examina desde o início; por exemplo, 3001 examina até 200 elementos a partir dessa posição. Não é um identificador Revit: a ordem de coleta pode mudar entre execuções, por isso a posição aproxima a região a investigar. Os limites de 20.000 pares e 60 segundos continuam válidos. Compare no Revit exemplos de DWG importado e vinculado com outros formatos, famílias e geometrias do hospedeiro; confira também o Clash 2D DWG, cuja lógica não foi alterada.

## Linhas e curvas no hospedeiro

O Conflitos em Links ignora elementos do projeto hospedeiro cuja classe Revit seja CurveElement, como linhas de modelo e detalhe; os elementos do link mantêm a seleção do usuário. A verificação por tipo é aplicada aos candidatos do hospedeiro; não se usa CurveElementFilter para excluir elementos, pois esse filtro seleciona curvas e pode descartar os demais elementos. Outros elementos sem sólido continuam sujeitos aos avisos aproximados; uma categoria com o nome "Lines" não é automaticamente removida se seu elemento não for CurveElement. Tubulações e eletrodutos reais são MEPCurve, não CurveElement.

O diagnóstico inclui a classe Revit dos elementos e identifica os elementos do link sem sólidos mais frequentes, por pares verificados. Esses dados ajudam a distinguir curvas reais de famílias ou outras classes que compartilham nomes de categoria, e a investigar um único elemento com muitos candidatos. Os números são limitados à amostra.

Valide em Revit com uma linha de modelo, uma linha de detalhe, tubos, eletrodutos, famílias com sólidos e malhas, comparando conflitos e desempenho. A exclusão por tipo ocorre após a triagem espacial. O desempenho final e a presença de conflitos conhecidos precisam ser validados no modelo real.

## Correção do filtro de curvas

Uma versão anterior aplicou `CurveElementFilter(CurveElementType.Invalid, true)` para tentar obter elementos que não fossem curvas. A documentação da API indica que ele seleciona curvas, e portanto não é seguro como filtro de exclusão geral. Ele foi removido da coleta e a checagem de `CurveElement` ocorre apenas após o elemento ser obtido. Valide no Revit com colisões físicas conhecidas; resultados de zero pares da versão anterior não são confiáveis.

## Cache compacta para elementos do link sem sólidos

Quando o elemento do link não tem sólidos, uma interseção de sólidos não pode ser calculada. Para preservar a verificação aproximada existente, o comando usa caixas do hospedeiro já extraídas anteriormente, sem reter uma cópia adicional dos sólidos. A cache é limitada a 4.096 hospedeiros e aceita somente entradas com até 8 caixas; conjuntos maiores continuam na rotina anterior, sem descarte de avisos. Os itens menos usados são removidos quando o limite é atingido.

O diagnóstico informa separadamente as reutilizações da cache compacta e, para os elementos do link sem sólidos com mais pares, mostra família, tipo, tamanho da caixa combinada em metros e número de partes. Essa informação ajuda a investigar se uma caixa muito ampla está produzindo candidatos dispersos; a caixa ou a família não é alterada automaticamente.

Um teste sintético percorre seis vezes 3.466 hospedeiros com uma caixa cada e verifica 3.466 extrações simuladas em vez de 20.796. Isso valida a política da cache, não estima o ganho real. Compare o diagnóstico na mesma posição e categorias no Revit, inclusive pares e avisos, e verifique colisões físicas conhecidas em um modelo pequeno antes de confiar na detecção em produção.

## Sólidos vazios e limites inválidos

O diagnóstico do modelo grande apresentou caixas com dimensões da ordem de 10²⁹ metros. A análise de caixas aproximadas consultava `GetBoundingBox()` inclusive em sólidos com zero faces e zero arestas. A Autodesk documenta a presença de sólidos vazios na geometria do Revit; eles não representam uma parte geométrica e são ignorados. Caixas invertidas ou com coordenadas não finitas são recusadas antes de transformar seus cantos, para não convertê-las em volumes enormes. Não há limite arbitrário de tamanho para caixas válidas.

Quando nenhuma parte utilizável está disponível ou uma parte não oferece caixa confiável, o comando tenta usar a caixa do próprio elemento como alternativa conservadora. Quando ela também está indisponível, o resumo indica elementos do link e pares do hospedeiro que podem não ter sido totalmente comparados; o diagnóstico lista exemplos. Uma análise com essas ocorrências não deve ser interpretada como garantia de ausência de conflitos. O diagnóstico informa ocorrências de sólidos vazios, incluindo possíveis releituras na extração de geometria.

Os testes cobrem caixas invertidas com valores extremos, NaN, caixas válidas e transformadas, além de distinguir sólidos completamente vazios de shells com faces ou arestas. Ainda é necessário validar os elementos afetados e colisões físicas conhecidas em Revit, comparar avisos e conferir os contadores de caixas indisponíveis. A causa exata das caixas impossíveis observadas depende dessa validação no modelo real.

## Câmeras internas de vista

O hospedeiro ignora somente elementos retornados pela API como `Element` genérico com categoria nativa `OST_Cameras`, identificados como câmeras não físicas. Famílias de equipamentos continuam elegíveis, inclusive se forem classificadas na categoria Cameras; elementos de outras categorias também permanecem. A verificação por tipo ocorre após a triagem espacial e não altera a geometria do modelo.

O diagnóstico inclui o ID da categoria do hospedeiro nos pares sem sólidos, para identificar casos em que um nome visual semelhante não corresponda à categoria nativa. A mensagem de geometria incompleta só é exibida quando há ao menos uma ocorrência não resolvida na amostra. Confirme no Revit que os IDs de câmera interna deixam de gerar avisos e que uma família física de câmera e colisões conhecidas continuam sendo encontradas; se esses IDs ainda aparecerem, compare o ID de categoria apresentado antes de ampliar o filtro.

## Reaproveitamento de regiões vazias

A coleta preliminar pode consultar uma região ampliada que contém integralmente a caixa do elemento do link. Somente se a mesma consulta nativa usada anteriormente não encontrar nenhum candidato nessa região ela pode dispensar a busca menor; regiões não vazias, fora dos limites, inválidas ou com falha usam o caminho anterior. A região efetivamente consultada é guardada e sua contenção é conferida antes de cada reutilização. A cache vive apenas durante aquela análise e é limitada a 2.048 regiões.

Para limitar o custo em modelos densos ou sem repetição espacial, a rotina restringe a frequência de novas consultas ampliadas; caixas que abrangem mais de duas células de 32 pés por eixo também usam a busca anterior. O resumo e o diagnóstico mostram consultas e reutilizações de regiões vazias. O ganho depende do modelo e não é garantido: regiões ampliadas ocupadas acrescentam trabalho à primeira consulta.

Os testes sintéticos comparam cada região dispensada com uma busca exaustiva em 10.000 consultas, incluindo contatos em limites positivos e negativos, caixas inválidas, regiões grandes, resposta desconhecida e falha de consulta. Em 500 consultas sintéticas idênticas em uma região vazia, uma consulta ampliada foi feita e 499 foram reutilizadas. Isso não substitui testes no Revit; compare os pares e volumes com a versão anterior, incluindo colisões físicas conhecidas, vínculos deslocados e rotacionados, famílias aninhadas, elementos grandes e amostras densas.

A mesma busca nativa também exclui previamente os IDs das câmeras internas que já seriam rejeitadas pela regra de tipo e categoria, usando `ExclusionFilter`. Somente os IDs identificados como `Element` genérico na categoria nativa de câmeras são filtrados; se a coleta falhar, a verificação por tipo continua e a rotina de regiões vazias não presume sucesso. Famílias de câmeras e outros elementos permanecem elegíveis. Teste colisões físicas conhecidas no Revit antes da utilização em produção.

## Somente conflitos físicos confirmados

A tabela principal e o CSV apresentam somente pares com interseção de sólidos cujo volume finito supera 1e-8 pés cúbicos. Contato sem volume positivo suficiente e sobreposição apenas de caixas não são reportados como conflitos. Se houver partes com falha, uma interseção positiva já obtida continua sendo uma confirmação física; sem confirmação, o par é separado como não verificado.

Elementos do link sem sólidos não são comparados por caixas na análise normal: são registrados como não verificados e a busca detalhada por seus candidatos é dispensada. Para candidatos sem sólidos no hospedeiro, geometrias parciais, falhas booleanas ou caixas indisponíveis, o resumo registra contagens e IDs identificáveis. A lista de itens não verificados é separada dos conflitos confirmados e limitada a 10.000 entradas, com aviso de truncamento; os contadores de ocorrências permanecem disponíveis. Na amostra diagnóstica, esses pares ainda podem ser investigados, mas nunca aparecem como conflitos confirmados apenas por caixas. A cache de caixas compactas da política anterior não é utilizada nessa comparação estrita.

**Limitação:** elementos sem sólidos e falhas de geometria podem esconder colisões reais. Em particular, o par visualmente confirmado no teste pequeno que só havia sido detectado por caixas deixará de aparecer na lista de conflitos confirmados; ele precisa aparecer nos itens não verificados para revisão. Ausência de conflito confirmado não é prova de que todos os elementos estão livres de conflito quando houver itens não verificados.

Teste em Revit com sólido-sólido intersectando, sólido-sólido separado, caixas sobrepostas sem colisão, contato sem volume, sólidos indisponíveis, malhas, geometrias parciais, falhas de operação, links rotacionados e famílias aninhadas. Compare o relatório de itens não verificados e confirme que o relatório principal e a exportação contêm somente volumes confirmados. Os testes sintéticos exercitam a classificação e a contabilização, mas não executam a API de geometria do Revit.

## Definições de segmento e sistemas MEP

A coleta do link e do hospedeiro ignora exclusivamente objetos da API identificados como `PipeSegment` ou `MEPSystem`. `PipeSegment` contém dados para preferências e tamanhos; `MEPSystem` representa o sistema lógico. Tubos, conexões, acessórios, dutos e eletrodutos modelados continuam elegíveis por seus próprios tipos. Categorias que contiverem apenas essas definições não serão oferecidas para seleção no link.

Em um modelo real, confirme com IDs conhecidos que os registros de configuração deixam de aparecer como não verificados, enquanto tubos físicos, conexões e uma colisão sólido-sólido continuam presentes. A identificação usa tipos da API, não o nome traduzido da categoria.

## Exclusão antecipada de curvas comprovadas

A análise tenta identificar até 10.000 linhas/curvas do hospedeiro durante até 2 segundos de preparação, verificando `element is CurveElement` para cada candidato antes de incluir o ID em um `ExclusionFilter`. O filtro `CurveElementFilter` é usado apenas para coletar candidatos; nunca é aplicado diretamente como filtro de exclusão do documento. Tubos, eletrodutos e famílias que não são CurveElement não entram na lista mesmo que a coleta os retorne por engano. Se a coleta for parcial, falhar ou não estiver disponível, a exclusão por tipo existente continua em vigor; uma chamada individual da API pode exceder o tempo previsto e o cancelamento aguarda o retorno dessa chamada.

A preparação, o número de IDs comprovados, o estado parcial e as reutilizações de regiões vazias são apresentados no diagnóstico. O limite não determina a elegibilidade final: ele só limita a otimização, e linhas fora da lista ainda são ignoradas pela verificação por elemento. Testes sintéticos exercitam candidatos mistos, limite, tempo e cancelamento; testes em Revit devem comparar conflitos físicos conhecidos, pares não verificados, tempo total e uso de memória. O ganho da preparação depende de quantas linhas aparecem nas buscas e pode ser menor do que o custo de preparação.
