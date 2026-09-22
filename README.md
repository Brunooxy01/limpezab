# limpezaB

Utilitário de terminal para manutenção local do Windows, em português.

## Recursos

- analisa espaço livre, temporários e consumo de memória;
- remove arquivos da pasta temporária do usuário com mais de 14 dias, após confirmação;
- localiza arquivos grandes nas pastas pessoais sem excluí-los;
- mostra processos e solicita encerramento normal por PID, sem encerramento forçado;
- oferece a opção **Executar agora** para iniciar imediatamente a manutenção completa;
- configura uma tarefa diária, semanal ou mensal no Agendador de Tarefas do Windows;
- registra um histórico local em `%LOCALAPPDATA%\LimpezaB\limpezaB.log`.

## Uso

Execute `limpezaB.exe` e escolha uma opção no menu. Para a execução agendada, o programa usa `limpezaB.exe --automatico`.

## Compilação

No Windows PowerShell 5.1 ou PowerShell 7:

```powershell
.\build.ps1
```

O projeto usa o compilador C# já disponível no PowerShell, sem exigir a instalação do SDK do .NET.

O ícone-fonte está em `assets/limpezaB.png`. Para regenerar o arquivo `.ico` com resoluções entre 16 e 256 pixels:

```powershell
.\tools\make-icon.ps1
```

## Segurança

O programa valida que arquivos removidos continuam dentro da pasta temporária, ignora links e arquivos de nuvem offline e testa se o arquivo está em uso. Arquivos pessoais grandes são apenas listados. Processos protegidos do Windows não podem ser selecionados e processos comuns só recebem uma solicitação de encerramento normal depois de confirmação explícita.
