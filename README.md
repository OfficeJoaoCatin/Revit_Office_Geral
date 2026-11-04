# Revit Family Importer

Plugin WPF para Autodesk Revit com ferramentas na aba **Office - Geral**:

- **Importar Familias**: importa familias carregaveis de outro arquivo `.rvt` ou `.rte` para o projeto atual.
- **Exportar Lote**: exporta PDF e DWG de multiplos projetos abertos na mesma sessao do Revit, com selecao de folhas, perfis, preview, fila, logs e checkpoint.
- **Salvar Tudo**: salva todos os documentos abertos que ja possuem caminho e estao modificados.
- **Sincronizar Centrais**: sincroniza modelos workshared abertos com seus respectivos arquivos centrais.

O plugin usa apenas Revit API. A exportacao nao usa `SendKeys`, automacao de janela, mouse, troca manual de janelas ou automacao externa.

## Funcionalidades

### Importar Familias

- Seleciona um arquivo Revit de origem (`.rvt` ou `.rte`).
- Lista familias carregaveis com nome, categoria e quantidade de tipos.
- Permite busca, selecao individual e selecao em massa.
- Importa para o documento ativo com progresso visual.
- Trata conflitos e erros por familia sem fechar o Revit.

### Exportacao em Lote

- Lista automaticamente projetos abertos via `UIApplication.Application.Documents`.
- Ignora documentos de familia, links e templates.
- Permite marcar quais projetos entram no lote.
- Permite selecionar folhas por modo: todas, somente emitidas ou manual.
- Exporta PDF pela API nativa `Document.Export` com `PDFExportOptions`.
- Exporta DWG pela API nativa `Document.Export` com `DWGExportOptions`.
- Le setups existentes de PDF e DWG quando disponiveis no projeto.
- Permite configurar PDF combinado, DPI, vetor/raster e centralizacao.
- Permite configurar setup DWG, versao DWG e mesclar vistas.
- Gera preview da estrutura final antes de exportar.
- Usa nomes por template, por exemplo:

```text
{ProjectName}/{Discipline}/{SheetNumber}
```

- Salva perfis em JSON no AppData.
- Processa uma fila de itens pequenos, sem paralelizar exportacoes.
- Possui cancelamento seguro: conclui o item atual, salva checkpoint e encerra.
- Salva checkpoints para retomar exportacoes interrompidas.
- Gera `ExportLog.txt` com data, projeto, folha, formato, duracao, memoria e erro.
- Exibe relatorio final com sucessos, falhas, arquivos gerados e botoes para abrir pasta/log.

### Salvar Tudo

- Percorre todos os documentos abertos no Revit.
- Salva documentos modificados com `Document.Save()`.
- Ignora documentos vinculados, somente leitura, sem caminho salvo ou sem alteracoes.
- Mostra resumo final com salvos, ignorados e falhas.

### Sincronizar Centrais

- Percorre modelos workshared abertos.
- Sincroniza com o central usando `Document.SynchronizeWithCentral`.
- Salva local antes e depois da sincronizacao.
- Faz relinquish dos elementos do usuario.
- Ignora documentos vinculados, nao workshared, somente leitura ou sem caminho de central.
- Mostra resumo final com sincronizados, ignorados e falhas.

## Arquitetura

```text
RevitFamilyImporter/
├── Commands/
│   ├── DocumentOperationReport.cs
│   ├── ImportFamiliesCommand.cs
│   ├── BatchExportCommand.cs
│   ├── SaveAllOpenDocumentsCommand.cs
│   └── SynchronizeOpenCentralModelsCommand.cs
├── Export/
│   ├── BatchExportRequest.cs
│   ├── DwgExporter.cs
│   ├── ExportCoordinator.cs
│   ├── ExportJob.cs
│   ├── ExportQueue.cs
│   ├── ExportWorker.cs
│   └── PdfExporter.cs
├── Models/
│   ├── BatchExportEnums.cs
│   ├── CheckpointData.cs
│   ├── DwgExportSettingsInfo.cs
│   ├── ExportDocumentInfo.cs
│   ├── ExportProgressInfo.cs
│   ├── ExportReport.cs
│   ├── FamilyItem.cs
│   ├── PdfExportSettingsInfo.cs
│   └── SheetInfo.cs
├── Profiles/
│   ├── BatchExportProfile.cs
│   └── ProfileService.cs
├── Services/
│   ├── CheckpointManager.cs
│   ├── ExportSettingsService.cs
│   ├── FamilyService.cs
│   ├── Heartbeat.cs
│   ├── MemoryMonitor.cs
│   ├── NamingService.cs
│   ├── PreviewService.cs
│   ├── SheetSelectionService.cs
│   ├── TimeoutController.cs
│   └── Watchdog.cs
├── UI/
│   ├── BatchExportViewModel.cs
│   ├── BatchExportWindow.xaml
│   ├── BatchExportWindow.xaml.cs
│   ├── ImportFamiliesWindow.xaml
│   ├── ImportFamiliesWindow.xaml.cs
│   └── RibbonIconFactory.cs
├── Utils/
│   ├── JsonStorage.cs
│   ├── Logger.cs
│   ├── ObservableObject.cs
│   └── RelayCommand.cs
├── App.cs
├── RevitFamilyImporter.addin
├── RevitFamilyImporter.2024.addin
└── RevitFamilyImporter.csproj
```

Fluxo da exportacao em lote:

```text
BatchExportCommand
  -> BatchExportWindow / BatchExportViewModel
  -> PreviewService + ProfileService + SheetSelectionService
  -> ExportCoordinator
  -> ExportQueue
  -> ExportWorker
  -> PdfExporter / DwgExporter
  -> Logger + CheckpointManager + MemoryMonitor + TimeoutController
```

## Requisitos

| Requisito | Versao |
|---|---|
| Revit 2024 | .NET Framework 4.8 |
| Revit 2025/2026 | .NET 8 |
| .NET SDK | 8.0 ou superior recomendado |
| Revit API | Instalada junto com a versao do Revit usada |

O projeto escolhe o alvo pelo parametro `RevitVersion`.

## Como Compilar

### Revit 2024

```powershell
dotnet build RevitFamilyImporter.csproj -c Release -p:RevitVersion=2024
```

Esse build usa:

```text
C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll
C:\Program Files\Autodesk\Revit 2024\RevitAPIUI.dll
```

### Revit 2025

```powershell
dotnet build RevitFamilyImporter.csproj -c Release -p:RevitVersion=2025
```

### Revit 2026

```powershell
dotnet build RevitFamilyImporter.csproj -c Release -p:RevitVersion=2026
```

A DLL gerada fica em:

```text
bin\Release\RevitFamilyImporter.dll
```

## Instalacao

### Revit 2024

Copie os arquivos compilados para:

```text
%APPDATA%\Autodesk\Revit\Addins\2024\
```

Arquivos esperados:

```text
RevitFamilyImporter.dll
RevitFamilyImporter.pdb
RevitFamilyImporter.2024.addin
```

O manifesto `RevitFamilyImporter.2024.addin` aponta para:

```text
%APPDATA%\Autodesk\Revit\Addins\2024\RevitFamilyImporter.dll
```

Se instalar em outro usuario ou pasta, ajuste o campo `<Assembly>` dentro do `.addin`.

### Revit 2025/2026

Para Revit 2025 ou 2026, copie a DLL e um `.addin` equivalente para:

```text
%APPDATA%\Autodesk\Revit\Addins\2025\
%APPDATA%\Autodesk\Revit\Addins\2026\
```

Depois reinicie o Revit. A aba **Office - Geral** deve mostrar os botoes **Importar Familias**, **Exportar Lote**, **Salvar Tudo** e **Sincronizar Centrais**.

## Como Usar

### Importar Familias

1. Abra o projeto de destino no Revit.
2. Acesse **Office - Geral**.
3. Clique em **Importar Familias**.
4. Selecione um arquivo `.rvt` ou `.rte` de origem.
5. Marque as familias desejadas.
6. Clique em **Importar Selecionadas**.
7. Acompanhe o progresso ate o resultado final.

### Exportar Lote

1. Abra no Revit todos os projetos `.rvt` que deseja exportar.
2. Acesse **Office - Geral**.
3. Clique em **Exportar Lote**.
4. Na aba **Projetos**, marque os projetos que entram na exportacao.
5. Na aba **Folhas**, escolha o modo de selecao e marque as folhas.
6. Na aba **Configuracoes**, defina pasta de destino, formatos, setups, nomenclatura, perfil, timeout e limite de memoria.
7. Confira a aba **Previa**.
8. Clique em **Exportar**.
9. Acompanhe a aba **Progresso**.

Se houver uma exportacao interrompida, o comando pergunta se deseja retomar a partir do ultimo checkpoint.

### Salvar Tudo

1. Abra os projetos ou familias que deseja salvar.
2. Acesse **Office - Geral**.
3. Clique em **Salvar Tudo**.
4. Revise o resumo final de documentos salvos, ignorados e falhas.

O comando salva apenas arquivos ja existentes em disco. Documentos novos sem caminho devem ser salvos manualmente com **Salvar como**.

### Sincronizar Centrais

1. Abra os modelos workshared que deseja sincronizar.
2. Acesse **Office - Geral**.
3. Clique em **Sincronizar Centrais**.
4. Aguarde a sincronizacao de cada modelo e revise o resumo final.

Este comando nao substitui a validacao de fluxo BIM da equipe. Use com cuidado em modelos centrais grandes ou em momentos de publicacao.

## Dados Salvos

Perfis e checkpoints sao salvos no AppData do usuario:

```text
%APPDATA%\BIM Tools\RevitFamilyImporter\BatchExportProfiles\
%APPDATA%\BIM Tools\RevitFamilyImporter\BatchExportCheckpoints\
```

O log da exportacao e salvo na pasta de destino escolhida:

```text
ExportLog.txt
```

## Observacoes Tecnicas

- A Revit API deve rodar na thread principal do Revit; por isso a exportacao nao e paralelizada.
- A fila processa unidades pequenas para reduzir travamentos: documento, folha/formato, liberar recursos, proximo item.
- Falha em uma folha nao cancela o lote inteiro.
- O timeout e verificado por item apos o retorno da chamada nativa de exportacao.
- A opcao `ExportLinks` de DWG e mantida no perfil/UI, mas a API usada localmente nao expoe uma propriedade direta correspondente em `DWGExportOptions`.
- Para PDF combinado, o plugin cria um arquivo por projeto.
- Os icones dos botoes do ribbon sao gerados por `RibbonIconFactory`, sem arquivos PNG externos.
- `Salvar Tudo` e `Sincronizar Centrais` sao comandos separados de proposito: salvar local e sincronizar central tem riscos e tempos diferentes.

## Solucao de Problemas

### O botao nao aparece

- Confira se o `.addin` esta na pasta da versao correta do Revit.
- Confira se o caminho `<Assembly>` aponta para a DLL correta.
- Feche e abra o Revit novamente.

### Erro de DLL do Revit ao compilar

- Verifique se a versao do Revit esta instalada.
- Compile com o parametro correto:

```powershell
dotnet build RevitFamilyImporter.csproj -c Release -p:RevitVersion=2024
```

### DLL bloqueada ao instalar

- Feche o Revit antes de substituir a DLL no AppData.
- Depois copie novamente os arquivos para a pasta `Addins`.

### Exportacao falha em uma folha

- Confira se a folha pode ser impressa.
- Confira se o setup PDF/DWG existe no projeto.
- Abra o `ExportLog.txt` para ver o erro registrado.

## Licenca

Uso interno. Revise, teste e valide os arquivos exportados antes de usar em entregas oficiais.
