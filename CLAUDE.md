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
```

**Antes de dizer que terminou: `dotnet build` e `dotnet test` passando.**

## Antes de mexer, leia

- **Onde cada coisa mora:** [`arquitetura.md`](./docs/referencia/arquitetura.md). Uma pasta por função, e
  `Connection`, `Protocol` e `Config` não conhecem as funções.
- **O que vai no fio:** [`protocolo.md`](./docs/referencia/protocolo.md). Campo, mensagem ou comando novo
  começa no SentinelWeb, não aqui.

## Regras do agente inteiro

- **O HWID é uma fila do firmware:** SHA-256 do serial do produto, senão o da placa, senão o UUID, o primeiro
  que não for de fábrica (lista do SentinelWeb), sem `-`, `:` e espaço, em minúsculas. Nada serve, não sobe;
  MAC e machine-id não entram. A fila só cresce no fim: mexer no começo duplica máquina cadastrada (A13).
- **Rodar de verdade é com sudo:** serial e UUID do DMI são só do root.
- **Comando é lista fechada:** `SHUTDOWN`, `REBOOT` e `SUSPEND`, cada um com um executável fixo e sem shell.
  O resto é recusado e vai para o log (A5).
- **Handshake primeiro, em toda conexão:** sem ele o comando não chega.
- **Endereço e segredos vêm da configuração, nunca do código:** `wss://` obrigatório (`ws://` só em
  `localhost`), sem opção de desligar a verificação do certificado. Cada máquina tem o seu token, que o
  cadastro troca pelo segredo de instalação; o agente apaga o segredo e não se recadastra sozinho (A11).
- **Nada escondido:** o agente aparece com o próprio nome no `ps` e no `systemctl status`, e para com
  `systemctl stop` (A6).
- **Buffer offline e lista de programas esperam o SentinelWeb:** o servidor precisa ler a hora da coleta
  (A9) e guardar os programas (A10) antes de o agente ligar os dois.
- **Só pt-BR** em log, comentário e docs.

## Não volta

Decidido; não proponha de novo:

- camuflagem de processo, proteção do binário e o driver de kernel `AssetManager.Shield` (A6);
- `LoggedOnUser` no handshake (A8);
- `Infra/` e `Modules/`, ou `Core`/`Linux`/`Worker` em projetos separados (A2).

## Docs e commits

- Antes de escrever em `docs/`, leia o [`docs/README.md`](./docs/README.md): a pasta se decide pelo
  ciclo de vida, não pelo assunto. Decisão nova continua a numeração (a próxima é a `A14`).
- Commit direto na `main`. Só `feat:` ou `chore:`, sem escopo, corpo em bullets curtos. Um assunto,
  um commit. Sem `Co-Authored-By` nem "Generated with".
