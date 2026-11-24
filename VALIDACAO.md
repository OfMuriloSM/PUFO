# Validação do PUFO 4.1.0-preview.1

## Estabilização de 24 de novembro de 2026

Restore concluído. Build Release com zero erros e avisos. Passaram 129 verificações, incluindo as 124 anteriores. Na rodada final, os tempos do Vosk foram 2.565 ms e 1.361 ms respectivamente para a primeira frase e a repetição.

A branch partiu de `ddf3622`. Mouse3 inicia a transcrição em português nos três estados. O interpretador e as preferências persistentes da assistente continuam bloqueados. Nenhuma frase aciona volume, mídia, interface, relógio ou clima por voz nesta build.

O modelo Vosk permanece em memória após a primeira frase até fechar a ilha. Cada frase usa um reconhecedor próprio. O encerramento libera o modelo após a última leitura. Na primeira rodada desta revisão, a mesma amostra sintética levou 3.147 ms com a carga inicial e 996 ms com o modelo residente. Os tempos incluem decodificação e excluem a captura. Essa escolha aumenta o uso de memória entre frases. A pausa de 900 ms e o limite de sete segundos seguem em vigor. A transcrição “lá” continua sem reprodução com microfone físico.

Fechar Configurações cancela o teste iniciado ali e preserva uma captura iniciada pela ilha. A suspensão do Windows solicita o cancelamento da sessão. A mensagem na ilha admite até três linhas com reticências. O teste de voz informa quando a voz selecionada desaparece.

A suíte usa janelas WPF reais, injeta a transição de fullscreen e simula a captura para verificar cancelamento e restauração. O teste Vosk usa áudio sintetizado em memória. A primeira leitura WASAPI retornou três grupos controláveis e zero fontes com pico naquela amostra. O acesso interativo ao desktop falhou ao inicializar o kernel com o erro 3 do Windows. Nenhum cenário físico foi homologado nesta revisão.

**Gravação no Windows**

1. Inicie o executável Release. Clique a rodinha, aguarde **Ouvindo**, diga “Satomi, que horas são?” e registre a transcrição exata e o retorno ao estado anterior. Repita sem o prefixo. A resposta com o horário segue pendente.
2. Em **Configurações → Assistente**, escolha a voz brasileira, use **Ouvir texto** e confira a saída selecionada, o mute e se a frase é audível. Repita e feche Configurações durante a fala. Confira se a reprodução para.
3. Inicie a escuta, cancele com Mouse3 e repita entrando em F11. Ao sair do fullscreen, confira se é possível iniciar outra captura. Repita após suspender e retomar o notebook.
4. Abra Spotify e YouTube, escolha uma sessão no painel e pause pelo controle visual. Registre qual player parou e se a seleção permanece após trocar de faixa. Repita com MP4/MKV quando o player publicar sessão. Confira mixer, saída e Wi-Fi pelos módulos. Repita a navegação a 100% e 150% de DPI e no segundo monitor, quando disponível.

A interface privada de seleção de saída depende da versão do Windows. A edição sem pacote oferece o acesso nativo às notificações. A inicialização automática exige distribuição instalada. A recuperação das escolhas de voz após reiniciar, a execução de comandos falados e a resposta com a hora real permanecem como critérios de aceite em aberto.

## Histórico da V4.0

A branch `feature/v4-voice` contém reconhecimento local por Mouse3 e uma bancada de teste de entrada, síntese e clima. Nesta revisão a ilha apresenta a transcrição. O interpretador de comandos, o executor e a persistência das preferências de voz continuam pendentes. Os controles da V3.2 seguem disponíveis.

## V4.0.1 parcial

Build Release concluído com zero erros e avisos. As 124 verificações passaram, incluindo as 122 anteriores.

A revisão parcial da V4.0.1 move o carregamento da gramática SAPI para fora do Dispatcher. A suíte inclui um reconhecimento simulado que entrega a transcrição após o segundo Mouse3 e verifica que a ilha encerra a operação sem apresentar a resposta atrasada.

Para verificar o limite atual no Windows, acione Mouse3 e diga separadamente “Que horas são?”, “Coloca o volume em 30%”, “Pausa a música”, “Abre as configurações” e “Como está o clima?”. A ilha apresenta somente a transcrição. Responder com a hora local, alterar o volume, pausar a sessão, abrir Configurações e consultar a cidade pela voz são critérios pendentes. O roteiro abaixo cobre Mouse3 e F11. As escolhas da bancada de voz são temporárias; a recuperação dessas preferências após reiniciar também está pendente.

## Ponto de recuperação

A tag de prévia `v3.2.0-preview.1` referencia o commit `55b674c` de `feature/v2-audio`, verificado com build Release e 100 verificações aprovadas antes da criação da branch V4.

## Resultado desta revisão

O build Release terminou com 0 erros e 0 avisos. As 122 verificações passaram neste notebook, incluindo as 100 regressões da V3.2. Uma voz brasileira instalada gerou “Que horas são?” diretamente em memória e o Vosk transcreveu “que horas são”. Os testes de ciclo de captura e cancelamento usaram entradas simuladas. O cliente de clima recebeu respostas HTTP locais de teste. A validação física do microfone, da reprodução audível, do F11 e da consulta meteorológica ao vivo está pendente.

| Área | Arquivos da revisão |
| --- | --- |
| Entrada, transcrição e síntese | `Services/Voz/MicrofoneWindows.cs`, `ReconhecimentoLocal.cs`, `FalaWindows.cs`, `SessaoVoz.cs` |
| Clima | `Services/ClimaOpenMeteo.cs` |
| Ilha e testes de voz nas Configurações | `MainWindow.Voz.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `MainWindow.Audio.cs`, `MainWindow.Configuracoes.cs`, `SettingsWindow.Voz.cs`, `SettingsWindow.xaml`, `SettingsWindow.xaml.cs` |
| Build e suíte | `PUFO.App.csproj`, `tests/PUFO.SmokeTests/Program.cs`, `tests/PUFO.SmokeTests/Voz.cs`, `VALIDACAO.md` |

As dependências novas são Vosk 0.3.38 e System.Speech 10.0.15. A síntese usa o contrato WinRT já disponível no projeto. O modelo acústico está fora do Git.

## Reconhecimento e síntese

- Mouse3 sobre a cápsula inicia uma fala de até sete segundos usando a entrada padrão. O primeiro pacote de áudio ativa o indicador. Uma pausa de aproximadamente 900 ms encerra a frase. Mouse3 novamente cancela a operação. A cápsula mostra a transcrição brevemente e retorna ao estado anterior.
- A captura usa NAudio em modo compartilhado, mono, PCM de 16 bits a 16 kHz. O Vosk carrega o modelo sob demanda, decodifica a frase localmente e libera o modelo. O áudio fica em memória durante a operação. A aplicação não cria arquivos de áudio, histórico de transcrições ou telemetria.
- Para usar o Vosk no Windows x64, extraia o modelo oficial `vosk-model-small-pt-0.3` em `%LocalAppData%\PUFO\modelos\vosk-model-small-pt-0.3`. O arquivo `final.mdl` deve ficar diretamente nessa pasta. O modelo português leve tem limitações com ruído e pronúncias diferentes. [Modelo português e licença](https://alphacephei.com/vosk/models)
- **Configurações → Assistente** permite testar uma entrada e escolher Vosk ou SAPI. O teste SAPI exige um reconhecedor instalado de cultura `pt-BR`. Instalar uma voz de síntese não instala necessariamente esse reconhecedor. O teste de saída lista as vozes brasileiras fornecidas por `Windows.Media.SpeechSynthesis`. As escolhas desses testes valem para o teste atual.
- O painel também consulta temperatura ou probabilidade máxima de chuva para a cidade digitada. Somente uma consulta explícita envia a cidade ao geocodificador e as coordenadas públicas da cidade ao serviço de previsão. Para um nome ambíguo, informe também estado e país, por exemplo `Fortaleza, Ceará, BR`. [Geocodificação](https://open-meteo.com/en/docs/geocoding-api) · [Previsão Open-Meteo](https://open-meteo.com/en/docs)

## Roteiro manual da V4

| Caso | Ação e resultado esperado |
| --- | --- |
| Microfone e retorno visual | Em Repouso, Prévia e Completo, clique a rodinha sobre a superfície. Aguarde **Ouvindo**, diga uma frase curta em português e faça uma pausa. Confira a transcrição e o retorno ao estado de origem. |
| Cancelamento e silêncio | Repita o clique durante a captura, durante o processamento e durante **Ouvir texto**. Confira o encerramento e a liberação da entrada antes da próxima tentativa. Permaneça em silêncio até o prazo expirar. |
| Ausência ou permissão | Em Assistente, selecione o microfone e teste após desconectá-lo ou suspender o acesso nas opções de privacidade do Windows. Verifique a mensagem e repita com uma entrada disponível. |
| Vozes locais | Escolha uma voz brasileira e use **Ouvir texto**. Em uma máquina sem voz compatível, confira a indicação. Teste SAPI apenas com reconhecedor brasileiro instalado. |
| Interação | Experimente Mouse3 em abas de navegador fora da ilha, clique esquerdo e direito, volume, mixer e Wi-Fi. Entre em F11 durante a captura e verifique que ela termina. Teste também redução de movimento. |
| Clima | Consulte uma localidade inequívoca, outra ambígua e uma inexistente. Desconecte a rede e confira o retorno de indisponibilidade. Compare o resultado real com o Open-Meteo para a mesma cidade. |

## Histórico da V3.2

Na versão `3.2.0-preview.1`, o hover de 500 ms abre os atalhos Iniciar, Áudio, Wi-Fi, Notificações e Configurações. O atraso e o tempo de fechamento seguem as preferências salvas. Com os padrões, a faixa aparece ao entrar em Áudio ou durante a prévia de volume. Quem habilitar **Priorizar a faixa durante a reprodução** também vê a mídia sob a fileira inicial. O painel completo mantém o mixer, o seletor de saída e as sessões de mídia da V3.1.

## Executar

```powershell
dotnet build PUFO.sln --configuration Release
dotnet run --project tests/PUFO.SmokeTests --configuration Release
dotnet run --project src/PUFO.App/PUFO.App.csproj --configuration Release
```

Execute a partir da raiz do projeto, no Windows, com o SDK .NET 10. Os dois primeiros comandos compilam a solução e executam a suíte. O terceiro inicia a aplicação para o roteiro manual.

A suíte usa um Dispatcher WPF real e preferências temporárias. Comandos de transporte, mudanças de volume e trocas de sessão nos testes usam dados simulados. As consultas ao catálogo GSMTC, ao endpoint e às sessões WASAPI são somente de leitura. A suíte também verifica o redimensionamento da cápsula, a seleção pela interface, o comportamento da barra de título e a recuperação do JSON por backup. As imagens em `artifacts/pufo-midia.png` e `artifacts/pufo-configuracoes.png` são renderizações WPF. A imagem do player usa dados de teste.

## Resultado da V3.2 em 23 de novembro de 2026

O build Release da solução terminou com **0 erros e 0 avisos**. A suíte executou **100 verificações aprovadas** nesta máquina. A consulta real encontrou **2 sessões GSMTC com título**, ambas fora de reprodução naquele momento. A leitura do mixer encontrou **5 grupos WASAPI** na saída padrão. A amostra das saídas ativas registrou **0 fontes com pico detectado**.

A detecção de duas fontes simultâneas, inclusive em saídas diferentes, foi exercitada com dados controlados. A suíte validou os atalhos, o redimensionamento por número de módulos, o arrasto do slider, a ordem das redes e o descarte de uma resposta atrasada. O estado de notificações sem pacote foi verificado no Windows. As verificações não enviaram comandos de mídia, não escreveram volume e não mudaram redes ou notificações.

## Prévia modular da V3.2

Uma lista ordenada de descritores fornece a identidade estável, o desenho vetorial, a disponibilidade, a ação e o painel de cada módulo. A fileira é medida pelo conteúdo. Uma única área contextual acompanha o módulo em foco. Ela aceita cliques, hover com breve tolerância e navegação por teclado. Com a ilha ativa, **F6** leva o foco à fileira, **Tab** percorre os atalhos e **Enter** abre o módulo e permite navegar por seus controles.

| Caso manual | Como verificar no notebook | Resultado esperado |
| --- | --- | --- |
| Hover e fechamento | Deixe o cursor no repouso por 500 ms com as preferências padrão. Passe de Áudio para Wi-Fi, volte à lista e desça do ícone até os controles. Saia da ilha e retorne antes do tempo de fechamento. Repita com Esc, clique direito e animações desativadas. | A fileira continua centralizada. Um painel ocupa a cápsula por vez. A passagem para o conteúdo e o retorno dentro da tolerância preservam a prévia. Esc e clique direito recolhem a ilha. |
| Áudio e arrasto | Em Áudio, confira o dispositivo e arraste o volume, inclusive para fora da cápsula. Abra **Mixer** e **Saídas**. Ajuste a tecla de volume enquanto Wi-Fi estiver aberto. | O arrasto chega ao fim, os atalhos abrem os controles existentes e o volume externo preserva o módulo escolhido. |
| Som simultâneo | Reproduza áudio contínuo no Spotify e em um vídeo do YouTube. Confira **Som detectado** em Áudio. Pause ou silencie um dos aplicativos e, se possível, direcione cada aplicativo a uma saída diferente pelo Windows. Teste também uma aplicação sem mídia publicada. | Cada fonte com pico aparece com a saída correspondente. Pausas curtas conservam o indicador. O silêncio sustentado retira a fonte após a retenção e a próxima amostra. Uma sessão WASAPI pode emitir som sem publicar uma reprodução GSMTC. |
| Reproduções | Com os dois players abertos, clique em **Reproduções**. Selecione uma faixa, mude a mídia do outro aplicativo e depois volte a **Seguir sessão ativa do Windows**. | A lista inclui as sessões publicadas, sua origem e seu estado. A escolha manual resiste às mudanças da outra sessão. Um navegador pode expor uma única sessão para várias abas. |
| Iniciar | Acione o primeiro ícone com o mouse. Em outro ciclo, abra **Mostrar PUFO** na bandeja, use F6 e Enter. Verifique em Windows 10 e 11, conforme disponibilidade. | O Menu Iniciar do sistema recebe foco. É possível digitar na pesquisa, sair com Esc e voltar ao aplicativo anterior normalmente. |
| Wi-Fi | Passe sobre Wi-Fi. Compare com as redes do Windows, use **Atualizar** e abra **Redes no Windows**. Repita com rádio desligado, adaptador ausente ou acesso à localização bloqueado, conforme disponível. | A rede conectada vem primeiro e as outras exibem o sinal informado pelo adaptador. Ausência, bloqueio e rádio desligado têm mensagens próprias. A seleção de rede e a conexão ficam a cargo do Windows. |
| Notificações | Abra o módulo nesta distribuição e use **Notificações no Windows**. Em eventual edição empacotada com a capacidade de leitura, teste consentimento concedido, negado e revogado. | No executável atual aparece o motivo da indisponibilidade. Em uma edição habilitada, somente **Permitir acesso** solicita consentimento. O conteúdo consultado é de leitura e sai da interface ao fechar o painel. |
| Configurações e foco | Clique na engrenagem da fileira e volte a abrir Configurações pela bandeja. Teste o menu, o clique fora do painel completo e a navegação com Tab e Shift+Tab. | Uma única janela de Configurações volta à frente. A engrenagem não abre o mixer. |
| Fullscreen e escala | Teste com uma janela maximizada e depois em F11. Repita em 100% e 150% de escala. Clique junto às extremidades da cápsula e na região externa da sombra. | O repouso cabe no topo, os painéis permanecem legíveis e os cliques fora da superfície chegam ao aplicativo de baixo. A ilha desaparece em fullscreen. |

### Origem e limites dos dados

- **Som detectado.** Enquanto o módulo Áudio está aberto, o PUFO lê em segundo plano as sessões nas saídas de reprodução ativas. O intervalo entre leituras é de 600 ms e a lista de saídas é revisada a cada 3 s. O indicador entra a partir de pico 0,003, permanece com pico de pelo menos 0,0015 e admite uma queda de até 1,8 s entre picos. Sons mais curtos que o intervalo de amostragem podem passar despercebidos. O mixer continua controlando a saída padrão e identifica sessões disponíveis, com som, silenciadas ou sem medidor. [Medidor de pico WASAPI](https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nf-endpointvolume-iaudiometerinformation-getpeakvalue)
- **Mídia.** O estado de reprodução e os comandos vêm do GSMTC. O Windows pode agrupar abas no mesmo processo e omitir capas ou medidores. A interface identifica a saída das fontes WASAPI e mantém a escolha manual entre sessões de mídia. [Sessões de transporte de mídia](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager)
- **Iniciar.** A ação chama o comando documentado `WM_SYSCOMMAND / SC_TASKLIST` em `DefWindowProcW`. Ela não sintetiza pressionamentos de teclas. [Comandos de janela do Windows](https://learn.microsoft.com/en-us/windows/win32/menurc/wm-syscommand)
- **Wi-Fi.** A Native Wi-Fi API fornece o último catálogo disponível por adaptador. A consulta roda fora da interface e limita-se à leitura. Em versões recentes do Windows, o próprio sistema pode apresentar a solicitação de localização na primeira consulta. **Redes no Windows** abre a página nativa para escolher uma conexão. [Lista de redes WLAN](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlangetavailablenetworklist) · [Acesso a Wi-Fi e localização](https://learn.microsoft.com/en-us/windows/win32/nativewifi/wi-fi-access-location-changes)
- **Notificações.** Esta distribuição WPF não tem identidade de pacote. Para habilitar a leitura, uma distribuição com identidade deve declarar `uap3:Capability Name="userNotificationListener"` no manifesto de pacote e obter consentimento. O painel consulta até oito notificações recentes quando há acesso. Não há coleta em segundo plano nem ação de exclusão. [Requisitos de UserNotificationListener](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/notification-listener)

### Arquivos da V3.2

| Área | Criados | Alterados |
| --- | --- | --- |
| Prévia e interação | `Models/ModuloPrevia.cs`, `MainWindow.Previa.cs`, `Controls/PainelConsulta.xaml` e seu code-behind | `MainWindow.xaml` e seu code-behind, `MainWindow.Configuracoes.cs`, `Controls/MedidorConteudo.cs` |
| Áudio e mídia | `Services/Audio/DetectorAtividade.cs`, `Services/Audio/MonitorSomWindows.cs` | `Services/Audio/MixerWindows.cs`, `MainWindow.Audio.cs`, `MainWindow.Midia.cs`, `Controls/LinhaMixer.xaml`, `Controls/CartaoMidia.xaml` e os dois code-behinds |
| Windows | `Services/AtalhosWindows.cs`, `Services/ConsultaPrevia.cs`, `Services/WifiWindows.cs`, `Services/NotificacoesWindows.cs` | |
| Versão e validação | `tests/PUFO.SmokeTests/PreviaModular.cs` | `PUFO.App.csproj`, `tests/PUFO.SmokeTests/Program.cs`, `tests/PUFO.SmokeTests/Midia.cs`, `VALIDACAO.md` |

Os caminhos de aplicação da tabela são relativos a `src/PUFO.App/`. A V3.2 usa o NAudio e os contratos Windows já referenciados pelo projeto. As capturas WPF estão em `artifacts/pufo-previa-modular.png`, `artifacts/pufo-previa-audio.png` e `artifacts/pufo-previa-wifi.png`. As duas últimas usam nomes e valores de teste.

## Histórico de validação da V3.1

| Verificação | Resultado observado |
| --- | --- |
| Build Release | 0 erros e 0 avisos |
| Suíte automatizada | 77 verificações aprovadas nesta máquina |
| Catálogo real de mídia do Windows | Consulta bem-sucedida, com 0 sessões publicadas no momento |
| Áudio real em modo de leitura | Endpoint disponível e 3 grupos de sessões WASAPI |
| Barra de título | Minimizar, maximizar, restaurar e fechar aprovados. O Windows distinguiu título, botão e borda. Foi verificado o modo PerMonitorV2 e a posição dos controles na área útil do monitor atual. |

A reprodução com aplicativos reais e a passagem física entre monitores permanecem pendentes. Os controles de transporte foram exercitados com sessões simuladas.

## Roteiro da V3.1 no notebook

| Cenário | Ação | Resultado esperado |
| --- | --- | --- |
| Configurações | Abra a ilha com clique esquerdo. Clique na engrenagem e depois em **Configurações** na bandeja. Minimize e abra por ambos os caminhos. | A mesma janela escura volta à frente. A engrenagem preserva o estado da ilha. A bandeja também oferece **Mostrar PUFO** e **Sair do PUFO**. |
| Barra e teclado | Arraste pelo título, dê duplo clique para maximizar e restaurar, use os três botões, redimensione pelas bordas e teste Tab, Espaço e Alt+F4. | O título integra a superfície escura. A janela maximizada respeita a área de trabalho. O foco acompanha a navegação e fechar as Configurações mantém o PUFO na bandeja. |
| Spotify para Windows | Reproduza uma música, passe o mouse sobre a ilha e abra o painel. Compare capa, título e artista. Pause, retome, avance e volte pelas faixas. Feche o Spotify. | A prévia acompanha a sessão em reprodução. A sessão pausada permite retomar no painel enquanto o Spotify a mantiver publicada. A troca de faixa atualiza o conteúdo. O encerramento retira a sessão quando o Windows a remove. |
| YouTube | Abra um vídeo no navegador usado no notebook, reproduza, pause e mude de vídeo. Compare título, miniatura, duração e posição. Tente avançar a posição quando o controle estiver habilitado. | O PUFO apresenta os dados e comandos anunciados pelo navegador. Uma live pode ter informações de tempo diferentes de um vídeo gravado. |
| Mais de uma sessão | Com Spotify e YouTube publicados, abra **Sessão**, escolha um deles e mude a faixa no outro. Depois selecione **Seguir sessão ativa do Windows**. Repita encerrando a sessão escolhida. | A escolha manual permanece apesar de novos metadados. O modo automático segue a sessão indicada pelo Windows. A remoção da escolhida dá lugar a outra sessão disponível. |
| Vídeos locais | Abra um MP4 e um MKV em cada player a avaliar. Confira título, tempo e estado, pause e tente arrastar o progresso. Feche o player durante uma troca. | Cada player aparece se publicar a sessão. Capa ausente usa ícone vetorial, duração ausente oculta a barra e comandos dependem das capacidades publicadas. Sem sessões, o painel volta ao áudio. |
| Áudio e mídia juntos | Durante uma faixa, abra o mixer, mova o volume geral e o de um aplicativo, alterne mute e saída e troque de faixa rapidamente. Em **Mídia**, alterne as três preferências e reabra o PUFO. | Os controles de áudio continuam operáveis. Com prioridade de mídia ativa, mudar o volume não abre a ilha durante a reprodução. Com a prioridade desligada e a prévia contextual de áudio ativa, a faixa pode compartilhar a prévia com o volume. Desativar apenas a prévia mantém a mídia no painel. As opções persistem. |
| Tela e foco | Em janela comum, maximizada e F11, confira a ilha. Alterne via Alt+Tab, inspecione a barra de tarefas e mova as Configurações entre monitores em 100% e 150%, se disponíveis. | A ilha fica no topo do monitor principal e se oculta em fullscreen. Só Configurações aparece no alternador e na barra de tarefas. Botões e bordas continuam utilizáveis após a mudança de escala. |

Para registrar cada reprodução real, anote o aplicativo e a versão, o arquivo ou tipo de vídeo, se a sessão apareceu, quais dados e comandos o Windows publicou e o resultado observado. Se o player não publicar uma sessão, registre **sessão indisponível** para aquele player e arquivo.

## Integração de mídia e limites

- A aplicação usa `Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager`. O alvo `net10.0-windows10.0.17763.0` permite ao SDK .NET referenciar os contratos oficiais disponíveis desde o Windows 10 versão 1809. A dependência de áudio continua sendo `NAudio.Wasapi`. [API de sessões de mídia](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager) · [APIs WinRT em aplicativos de desktop](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/winrt-apis-desktop-apps)
- Quem publica as informações é o aplicativo que reproduz. O Spotify pode omitir comandos para certos conteúdos. No YouTube, a origem pode ser o navegador e várias abas podem compartilhar a mesma sessão. Em arquivos locais, a disponibilidade depende do player e de seus metadados. A extensão MP4 ou MKV, isoladamente, não determina a integração.
- O seletor lista as sessões publicadas. A seleção manual vale durante a vida daquela sessão. Sem título, a interface usa o identificador real de origem informado pelo Windows. Sem posição e duração válidas, a barra de progresso fica oculta. Quando o player fornece tempo e desabilita a busca, o progresso fica disponível apenas para leitura.
- A descoberta, a capa e o estado acompanham notificações do Windows. O relógio local interpola o progresso uma vez por segundo quando o painel de mídia está visível e em reprodução. Uma falha no catálogo limpa os controles obsoletos e agenda nova tentativa após 30 segundos.

## Regressões de áudio, ilha e preferências

1. Deixe o ponteiro no topo por 500 ms. Saia e retorne antes de 500 ms para cancelar o fechamento. Teste clique fora, clique direito, Esc e a área transparente. Pressione as teclas de volume repetidamente e compare com o valor do Windows durante e após arrastar.
2. Abra o painel de áudio. Use a seta para expandir e recolher o mixer durante a reprodução. Observe a borda inferior e confirme que ela acompanha o conteúdo. Abra e feche aplicativos com áudio, teste mute individual, sons do sistema e a rolagem com vários aplicativos.
3. Abra o seletor, avance pelas saídas com Tab e confirme com Enter ou Espaço. Confira a marca na saída padrão após a leitura do Windows. Desconecte e reconecte um dispositivo e teste o atalho **Som no Windows**. Repita com mais saídas para conferir a rolagem e com nomes longos.
4. Na bandeja, abra **Configurações** duas vezes. Confirme que a mesma janela volta à frente e aparece no Alt+Tab e na barra de tarefas. Abra um navegador em F11 e confirme que a ilha some e volta ao sair. A janela de configurações deve permanecer aberta. Verifique **Mostrar PUFO** e **Sair do PUFO** na bandeja.
5. Em **Ilha**, teste hover de 300 ms, fechamento de 800 ms e animações em 150%. Desligue as animações e confira que as transições passam a concluir imediatamente. Repita com a preferência do Windows de reduzir movimento ativada e com mais de uma escala de DPI.
6. Em **Áudio**, desligue a prévia contextual e mude o volume no Windows. A ilha em repouso só deve abrir por interação do usuário. Abra o painel manualmente e teste o mixer com a preferência de abertura expandida. Verifique a saída atual e os estados em **Módulos**.
7. Em **Geral**, abra a pasta de configurações. Encerre o PUFO pela bandeja, abra novamente e confira os valores salvos. A execução a partir do projeto deve informar que a inicialização com o Windows requer uma versão instalada.

## Comportamento e limites

- Mixer e seletor mantêm seu conteúdo durante o recolhimento. A altura da cápsula acompanha a altura efetivamente revelada e o conteúdo excedente usa rolagem interna. Leituras de volume atualizam os valores sem reiniciar as transições.
- O volume geral usa o endpoint padrão de reprodução de multimídia. O mixer enumera as sessões desse endpoint enquanto está aberto. Agrupa por processo e identificador ou grupo do Core Audio, separando os sons do sistema. O símbolo `~` indica a média de sessões com volumes diferentes. Não há garantia de controle por aba do navegador.
- A seleção usa `IPolicyConfig`, uma interface COM privada. O PUFO confirma os papéis Console e Multimídia, preserva Comunicações e oferece **Som no Windows** se a troca falhar. [Atalhos de Configurações do Windows](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings)
- As preferências são gravadas em `%LocalAppData%\PUFO\settings.json` após 600 ms sem mudanças ou no fechamento. A substituição do arquivo mantém uma cópia válida em `settings.json.bak`. Os atrasos ficam entre 100 e 2.000 ms, a velocidade entre 50% e 200%. O tema Preto PUFO é o único disponível.
- Esta build de desenvolvimento não pode ativar o autostart. Uma futura distribuição Release instalada por usuário pode habilitar a integração usando `PufoDistribuicaoInstalada=true` no build e o executável em `%LocalAppData%\Programs\PUFO\PUFO.App.exe`. Somente a escolha explícita do usuário grava o valor PUFO em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. A remoção é restrita à entrada criada pelo PUFO. [Chaves Run e RunOnce](https://learn.microsoft.com/pt-br/windows/win32/setupapi/run-and-runonce-registry-keys)
- A validação com reprodução real em Spotify, YouTube e players locais, gestos físicos, F11, Alt+Tab e monitores com escalas diferentes depende da sessão interativa do notebook.
