# SentinelAgente 🛰️
> **O agente do SentinelWeb: inventário, telemetria e comando nas máquinas Linux**

O **SentinelAgente** roda como serviço em cada computador, conecta no `/agent-hub` do
[SentinelWeb](https://github.com/pauloeduas-software/SentinelWeb), apresenta a máquina, com as placas de vídeo, manda o uso de CPU,
memória e disco de hora em hora e executa os comandos que a tela oferece:
desligar, reiniciar e suspender.

## 📁 Arquitetura

Um projeto, com uma pasta por função:

```
SentinelAgente/
├── Program.cs
├── Config/ · Connection/ · Protocol/          # a base: as funções usam, e elas não conhecem as funções
├── Identity/ · Inventory/                    # quem é a máquina e o que ela tem
├── Telemetry/  Cpu · Memory · Disk
└── Commands/                                  # desligar, reiniciar, suspender
tests/                                         # as mesmas pastas, com testdata/
packaging/                                     # o pacote .deb
```

**A convenção completa está em [`docs/referencia/arquitetura.md`](./docs/referencia/arquitetura.md).**
Leia antes de criar arquivo novo.

## 🚧 Estado

A conexão com o SentinelWeb está pronta (`Config/`, `Protocol/` e `Connection/`): endereço na configuração,
`wss://`, um token por máquina, que o cadastro troca pelo segredo de instalação, e reconexão. Ela entra no
`Program.cs` junto com o `Identity/` e o `Inventory/`, que montam o handshake, a `Telemetry/` e os
`Commands/`. Só vai o que o SentinelWeb lê: programas, rede e processos ficam de fora até lá guardar e
mostrar (A24). O contrato que o código precisa cumprir está em
[`docs/referencia/protocolo.md`](./docs/referencia/protocolo.md), e o ambiente, em
[`docs/guias/preparar-o-ambiente.md`](./docs/guias/preparar-o-ambiente.md).
