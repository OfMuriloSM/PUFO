# PUFO - código-fonte limpo da V2

Este pacote foi montado a partir do projeto `PUFO.zip` enviado nesta conversa.

## Correção aplicada

- Removida a classe duplicada `src/PUFO.App/States/Audio/ServicoAudioWindows.cs`.
- Mantida `src/PUFO.App/Services/Audio/ServicoAudioWindows.cs`, que inclui leitura, volume e mudo.
- Não incluídos arquivos temporários WPF (`*_wpftmp.csproj`), diretórios `obj`, `bin`, `.git` ou `.vscode`.
- Os demais arquivos de código são preservados como enviados, sem alterações de lógica.

## ATENÇÃO para projeto existente

Extrair um ZIP por cima de uma pasta **não remove** arquivos que já existem. Para corrigir a pasta de trabalho original, primeiro exclua a duplicata pelo terminal na raiz do projeto:

```powershell
Remove-Item -LiteralPath 'src/PUFO.App/States/Audio/ServicoAudioWindows.cs'
dotnet build PUFO.sln
```

Não há necessidade de substituir os demais arquivos para resolver CS0101/CS0111.

## Teste como pasta nova

Extraia o pacote para uma pasta nova, entre na pasta `PUFO`, e execute:

```powershell
dotnet restore PUFO.sln
dotnet build PUFO.sln
dotnet run --project src/PUFO.App/PUFO.App.csproj
```

Este pacote não contém o histórico Git local. Continue usando o repositório original para commits e push.
A compilação e execução WPF dependem do Windows; não foram executadas aqui.
