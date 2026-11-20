# CLAUDE.md

Mapa para agentes de código, curto de propósito: a fonte é a [`docs/`](./docs/README.md), e aqui fica
onde olhar e o que não se negocia. Mudou uma regra daqui? Atualize este arquivo no mesmo commit.

## O que é

O agente do [SentinelWeb](https://github.com/pauloeduas-software/SentinelWeb): um serviço do systemd, em C#
(.NET 10), que manda inventário e telemetria pelo WebSocket `/agent-hub` e executa os comandos que chegam por
ele. O protocolo é do SentinelWeb; este repositório só fala o que está escrito lá.

## Comandos

```bash
dotnet build     # compila
dotnet test      # os testes (xUnit v3 pelo Microsoft.Testing.Platform, ligado no global.json)
dotnet format    # formata pelo .editorconfig
./packaging/build-deb.sh   # o .deb em artifacts/ (A19)
```

**Antes de dizer que terminou: `dotnet build` e `dotnet test` passando, e o `dotnet format` sem mudar nada.** O CI
(`.github/workflows/ci.yml`) cobra os três a cada push na `main` e monta e confere o `.deb` de amd64 e arm64.

## Antes de mexer, leia

- **Onde cada coisa mora:** [`arquitetura.md`](./docs/referencia/arquitetura.md). Uma pasta por função, e
  `Connection`, `Protocol` e `Config` não conhecem as funções. O `ArchitectureTests.cs` reprova quem quebra
  isso, e o `DocumentationLinksTests.cs` o link ou a âncora que não existe (`tests/Repository/`).
- **O que vai no fio:** [`protocolo.md`](./docs/referencia/protocolo.md). Campo, mensagem ou comando novo
  começa no SentinelWeb, não aqui.

## Regras do agente inteiro

- **O HWID é o id da instalação:** um UUID sorteado no primeiro arranque e guardado em
  `/var/lib/sentinel-agent/hwid`, como o `agentid` do GLPI. Quem reconhece a máquina reinstalada é o SentinelWeb,
  pelo `BiosSerial` (o do produto, senão o da placa) e, quando os dois lados têm, pelo `SystemUuid`; valor de
  fábrica não vai (A20).
- **Rodar de verdade é com sudo:** o HWID e o token moram em `/var/lib/sentinel-agent`, só do root, e sem acesso
  ao HWID o agente não sobe; serial e UUID do DMI também são só do root.
- **Comando é lista fechada:** `SHUTDOWN`, `REBOOT` e `SUSPEND`, cada um com um verbo fixo do
  `/usr/bin/systemctl`, sem shell.
  O resto é recusado e vai para o log, e o resultado do `systemctl` também; os bloqueios do logind valem (A5, A18).
  A lista não cresce (A23). O `REFRESH` não é comando de sistema: a conexão o trata, e ele só faz o agente mandar
  o handshake e a telemetria na hora (A26).
- **Handshake primeiro, em toda conexão, e de novo uma vez por dia:** sem ele o comando não chega, e ele leva
  a versão do agente (A25).
- **Endereço e segredos vêm da configuração, nunca do código:** `wss://` obrigatório (`ws://` só em
  `localhost`), sem opção de desligar a verificação do certificado. Cada máquina tem o seu token, que o
  cadastro troca pelo segredo de instalação; o agente apaga o segredo e não se recadastra sozinho (A11).
- **Nada escondido:** o agente aparece com o próprio nome no `ps` e no `systemctl status`, e para com
  `systemctl stop` (A6).
- **Só vai o que o SentinelWeb lê:** programas, a telemetria de rede e os processos não vão, e voltam quando o
  servidor guardar e mostrar (A24); as placas de vídeo levam o nome, e a dedicada, a memória quando o driver
  mostra (A28).
- **Nunca dado verdadeiro no repositório:** testdata, exemplo e doc são inventados no formato real; nada
  copiado da máquina, da rede ou da conta (MAC, IP, hostname, serial, processos, programas).
- **Só pt-BR** em log, comentário e docs.

## Não volta

Decidido; não proponha de novo:

- camuflagem de processo, proteção do binário e o driver de kernel `AssetManager.Shield` (A6);
- buffer offline da telemetria, enquanto ela for de hora em hora (A17);
- `LoggedOnUser` no handshake (A8);
- comandos de terminal e ações de sistema novas além das três de energia (A23);
- `InstalledSoftware`, `Network` e `TopProcesses` antes de o SentinelWeb guardar e mostrar (A24);
- `Infra/` e `Modules/`, ou `Core`/`Linux`/`Worker` em projetos separados (A2).

## Docs e commits

- Antes de escrever em `docs/`, leia o [`docs/README.md`](./docs/README.md): a pasta se decide pelo
  ciclo de vida, não pelo assunto. Decisão nova continua a numeração (a próxima é a `A29`).
- **Decisão do SentinelWeb não se cita aqui**, nem a daqui lá: o que o agente precisa saber do servidor vai
  no `protocolo.md`, como fato.
- Commit direto na `main`. Só `feat:` ou `chore:`, sem escopo, corpo em bullets curtos. Um assunto,
  um commit. Sem `Co-Authored-By` nem "Generated with".
