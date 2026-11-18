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
