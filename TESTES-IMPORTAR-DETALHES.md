# Testes — Importar Detalhes

Use uma cópia do modelo Revit e um DWG de teste conhecido. Antes de testar, feche processos do GstarCAD que não façam parte do teste. Instale a DLL e as dependências junto com `OfficeGstarCadWorker.exe` e `OfficeGstarCadWorker.exe.config` na mesma pasta, e inicie o Revit novamente. O auxiliar não solicita o fechamento do GstarCAD ou dos seus documentos, mesmo ao concluir com sucesso. Examine a sessão e feche-a manualmente quando não for necessária.

## Fluxo normal

1. Com o GstarCAD fechado, selecione alta fidelidade e importe um detalhe simples. Verifique a vista, o desenho, o relatório, as fontes e o caminho temporário.
2. Importe vários detalhes com blocos, atributos, hachuras e tipos de linha, incluindo nomes acentuados. Compare visualmente com o DWG de origem e confirme o reaproveitamento da sessão pelo log.
3. Importe os mesmos detalhes pelo motor interno e compare. A geração interna continua com limitações de fidelidade.
4. Abra o GstarCAD com outro arquivo, selecione alta fidelidade e teste **Cancelar importação**. Confirme que nenhuma vista foi criada e que o arquivo e a opção de alta fidelidade continuam intactos. Repita e escolha **Continuar com motor interno**.
5. Teste com GstarCAD 2022, 2023 e outras versões efetivamente disponíveis. A escolha segue os ProgIDs COM do Windows; o comando não seleciona necessariamente a versão mais recente. Verifique a versão e o PID no log. Se a identificação da janela não for suportada, a rotina deve informar falha e preservar o CAD.

## Simular problemas

1. Execute `dotnet run --project tests\WorkerClientChecks\WorkerClientChecks.csproj -c Release`. O processo simulado cobre sucesso, erro, auxiliar sem resposta, cancelamento, tempo limite curto e reaproveitamento após erro, sem iniciar o CAD real.
2. Durante a geração com o CAD real, clique **Cancelar**. Confirme que o Revit volta a responder, as vistas já concluídas permanecem e nenhuma vista é criada para o detalhe interrompido. A operação do próprio Revit não pode ser interrompida no meio; o cancelamento é observado ao retornar dessa chamada.
3. Em uma cópia do modelo, interrompa apenas `OfficeGstarCadWorker.exe` pelo Gerenciador de Tarefas durante a geração. Confira a falha no relatório e confirme que o Revit continua acessível. Não encerre o Revit nem um processo `gcad` do usuário para esse teste.
4. Se houver um diálogo de licença ou de confirmação do CAD em ambiente de teste, deixe-o sem resposta por mais de 120 segundos. Confira se o auxiliar é interrompido, se o relatório informa o limite e se o CAD remanescente pode ser fechado manualmente. Não provoque perda de trabalho nem altere arquivos reais para produzir esse cenário.
5. Selecione um recorte maior que o limite de entidades configurado. Ele deve falhar sem conversão silenciosa pelo motor interno. Teste uma geometria de grande extensão para avaliar o consumo de memória.
6. Teste um lote grande, medindo o tempo por detalhe e a memória. A leitura inicial e a importação/regeneração dentro do Revit continuam síncronas e podem demorar; o limite se aplica à geração pelo auxiliar, não à API do Revit nem ao motor interno.

Logs: `%LOCALAPPDATA%\RevitFamilyImporter\Logs\DetailImport_YYYYMMDD.log` e `DetailImportWorker_YYYYMMDD.log`. Os recortes temporários ficam em `%TEMP%\RevitFamilyImporter\DetailImports`. Guarde o relatório, os logs e o DWG de teste se ocorrer uma falha.
