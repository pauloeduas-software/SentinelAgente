# SentinelAgente 🛰️
> **O agente do SentinelWeb: inventário, telemetria e comando nas máquinas Linux**

O **SentinelAgente** roda como serviço em cada computador, conecta no `/agent-hub` do
[SentinelWeb](https://github.com/pauloeduas-software/SentinelWeb), apresenta a máquina, com as placas de
vídeo, manda o uso de CPU, memória e disco de hora em hora, executa os comandos que a tela oferece
(desligar, reiniciar e suspender) e atende ao Atualizar agora, que pede os dados na hora.

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

Pronto e ligado inteiro no `Program.cs`. A conexão (`Config/`, `Protocol/` e `Connection/`) tem o endereço
na configuração, `wss://`, um token por máquina, que o cadastro troca pelo segredo de instalação, e
reconexão. O handshake sai do `Identity/` e do `Inventory/`, a amostra de uso da `Telemetry/`, e os comandos
de energia dos `Commands/`. Só vai o que o SentinelWeb lê: programas, a telemetria de rede e os processos ficam de fora até
lá guardar e mostrar (A24).

O mapa da documentação está em [`docs/README.md`](./docs/README.md): o contrato com o servidor em
[`docs/referencia/protocolo.md`](./docs/referencia/protocolo.md), o ambiente em
[`docs/guias/preparar-o-ambiente.md`](./docs/guias/preparar-o-ambiente.md) e a instalação em
[`docs/guias/instalar-o-agente.md`](./docs/guias/instalar-o-agente.md).

## 📄 Licença

**[GNU GPL, versão 3 ou posterior](./LICENSE)** (`GPL-3.0-or-later`). Qualquer um pode usar, alterar e
redistribuir. Quem distribuir o agente, alterado ou não, oferece o código-fonte junto, com a mesma licença
(A29).
