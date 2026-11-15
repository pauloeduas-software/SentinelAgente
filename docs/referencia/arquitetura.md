# Arquitetura do SentinelAgente

> Convenção de código do projeto. Vale para todo arquivo novo.
> Modelo de referência: uma pasta por função, como o `pkg/` do Fleet (orbit), e um coletor por métrica,
> como os `plugins/inputs` do Telegraf ([A2](../decisoes/plataforma.md#a2--um-projeto-com-uma-pasta-por-função)).

---

## A regra que sustenta tudo

**Um projeto, e uma pasta por função, direto na raiz dele.** Não há camada por cima (`Core`, `Infra`,
`Modules`) nem pasta por tipo de arquivo (`Interfaces/`, `Models/`, `Services/`): quem procura como o
comando funciona abre `Commands/` e acha tudo, a interface inclusive.

Três pastas são **base**, e as outras as usam:

```
Commands · Identity · Inventory · Software · Telemetry · Service   ──▶   Connection · Protocol · Config
```

- `Connection`, `Protocol` e `Config` **não** conhecem nenhuma função: não importam nada de `Commands`,
  `Identity`, `Inventory`, `Software`, `Telemetry` nem `Service`.
- Uma função pode usar outra quando precisa do dado dela (o `Inventory` usa o HWID do `Identity`), nunca o
  contrário em ciclo.
- O `Program.cs` é o único lugar que conhece todas: ele liga as peças.

Um `using` que sobe a seta é o sinal de que algo está na pasta errada.

---

## As pastas

```
SentinelAgente/
├── SentinelAgente.slnx            # a solução: o agente e os testes
├── Directory.Build.props          # o que vale para todo projeto: net10.0, nullable, aviso vira erro
├── global.json                    # o SDK 10 e o dotnet test pelo Microsoft.Testing.Platform
├── SentinelAgente/                # o agente; o binário sai como `sentinel-agent`
│   ├── Program.cs                 # O PROCESSO: host, systemd e o registro das peças
│   ├── Config/                    # BASE. endereço do servidor, token, intervalo da telemetria
│   ├── Connection/                # BASE. o WebSocket com o /agent-hub: token, handshake a cada conexão, reconexão
│   ├── Protocol/                  # BASE. o envelope e as mensagens, espelho do agent-protocol.types.ts
│   ├── Identity/                  # o HWID, sorteado na instalação (A20)
│   ├── Inventory/                 # o que o handshake leva: nome, sistema, rede, serial, fabricante, RAM (A14)
│   ├── Software/                  # os programas instalados, pelo dpkg ou rpm (A10)
│   ├── Telemetry/                 # a amostra periódica: um coletor por métrica
│   │   ├── Cpu/
│   │   ├── Memory/
│   │   ├── Disk/
│   │   ├── Network/
│   │   ├── Processes/             # quais programas estão rodando
│   │   └── Gpu/                   # uma leitura por fabricante: NVIDIA, AMD, Intel
│   ├── Commands/                  # a lista fechada de ações e quem roda o systemctl (A5, A18)
│   └── Service/                   # a vida do serviço: iniciar, parar, avisar o systemd
├── tests/                         # as mesmas pastas do agente
│   └── testdata/                  # /proc e /sys inventados no formato real, para os leitores
├── packaging/                     # o .deb: a unidade do systemd, os scripts de instalar e remover e o build-deb.sh (A3, A19)
└── docs/
```

### Dentro de `Telemetry/`

Cada métrica é uma pasta com o seu coletor, e todos entregam a sua parte da mesma amostra. Métrica nova é
pasta nova, sem mexer nas outras — é como o Telegraf cresce (`plugins/inputs/cpu`, `mem`, `disk`, `net`).
Cada coletor implementa o `ITelemetryCollector`, preenche a sua parte do `TelemetrySample` e entra no
`TelemetryRegistration`; quem marca a hora e manda é o `TelemetryService`. Os coletores leem `/proc` e `/sys`
pelo `SystemRoot` do `Inventory`.

A GPU é a exceção que confirma a regra: dentro dela, uma leitura por fabricante, porque NVIDIA, AMD e Intel
expõem o uso de formas diferentes.

### Onde mora o que é do Linux

Na pasta da função que precisa dele: quem lê `/proc/stat` é o coletor de `Cpu/`, e quem chama `systemctl`
é o `Commands/`. O agente é só Linux (a especificação de requisitos diz isso), então não há camada que
esconda o sistema operacional. Se o Windows vier, cada função ganha a sua versão ao lado da do Linux.

### Testes

O projeto de teste repete as pastas do agente. Quem lê arquivo do sistema recebe o caminho base por
parâmetro, e o teste aponta para `testdata/` — o teste não depende da máquina que o roda. O que está em
`testdata/` é inventado no formato real, nunca copiado de uma máquina: uma máquina estranha que deu problema
vira um exemplo com o mesmo formato e valores inventados, e um teste.
