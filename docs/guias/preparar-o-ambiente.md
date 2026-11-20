# Preparar o ambiente

> Guia. Receita para quem já sabe o que quer fazer — o *porquê* está na
> [A1](../decisoes/plataforma.md#a1--o-agente-recomeça-do-zero-em-c-com-net-10) (o .NET 10) e na
> [A20](../decisoes/protocolo.md#a20--o-hwid-é-o-id-da-instalação-e-quem-reconhece-a-máquina-é-o-sentinelweb-como-no-glpi)
> (o sudo).

---

## O SDK

```bash
sudo apt install dotnet-sdk-10.0     # no Ubuntu 24.04 ou mais novo, o .NET 10 vem do próprio Ubuntu
dotnet --version                     # 10.0.100 ou outro 10.0 mais novo
```

O `global.json` pede o SDK 10: com só outra versão instalada, o `dotnet build` recusa e diz qual falta.

## Compilar e testar

```bash
dotnet build
dotnet test
dotnet format
```

O `dotnet test` roda pelo Microsoft.Testing.Platform, ligado no `global.json`. O `dotnet format` arruma o código
pelo `.editorconfig`; o CI roda com `--verify-no-changes` e reprova o que ele mudaria.

## Um servidor para conversar

O agente precisa do SentinelWeb de pé, montado como o `README.md` de lá ensina (o `.env`, o
`docker compose up -d` e o `bun run db:migrate`). No repositório dele:

```bash
bun run dev              # a API sobe na 3001 (a PORT do .env)
```

O endereço é `ws://localhost:3001/agent-hub`. O segredo de instalação é o `AGENT_ENROLL_SECRET` do `.env` do
SentinelWeb; vazio, em desenvolvimento o servidor aceita qualquer um, mas o arquivo do agente precisa de um
texto qualquer: o agente recusa o arquivo vazio. O agente o troca pelo token da máquina
no primeiro contato e apaga o arquivo.

## Rodar o agente

Com sudo: o HWID mora em `/var/lib/sentinel-agent`, que é só do root, e sem ele o agente não sobe. O serial e
o UUID do firmware também são só do root; sem eles o agente roda, mas o SentinelWeb nem liga a máquina ao
computador pela série nem reconhece a máquina reinstalada. Em desenvolvimento, a linha de comando faz o papel do `/etc/sentinel-agent/agent.json`. Com o
pacote instalado nesta máquina, pare o serviço antes (`sudo systemctl stop sentinel-agent`): os dois usam o
mesmo HWID, e o cadastro do segundo leva 409.

```bash
dotnet build                         # sem sudo: compilar como root deixa bin/ e obj/ do root
echo "<AGENT_ENROLL_SECRET>" > /tmp/sentinel-secret && chmod 600 /tmp/sentinel-secret
sudo dotnet run --no-build --project SentinelAgente -- \
  --Server:Url=ws://localhost:3001/agent-hub \
  --Server:EnrollSecretFile=/tmp/sentinel-secret \
  --Server:TokenFile=/tmp/sentinel-token
```

O primeiro contato troca o segredo pelo token e apaga o arquivo do segredo; dali em diante, bastam o
`--Server:Url` e o `--Server:TokenFile` (o `/tmp` some no reboot, e aí o recadastro pede o segredo de novo).
O HWID é sorteado na primeira vez e fica em `/var/lib/sentinel-agent/hwid`, também na máquina de
desenvolvimento. Apagá-lo sorteia outro, e o token, preso ao HWID antigo, passa a ser recusado com 1008: o
recadastro pede o segredo de instalação de novo. Instalado pelo pacote, o endereço vem do `agent.json`, e o
segredo e o token ficam nos caminhos padrão.
