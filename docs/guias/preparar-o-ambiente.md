# Preparar o ambiente

> Guia. Receita para quem já sabe o que quer fazer — o *porquê* está em
> [`../referencia/arquitetura.md`](../referencia/arquitetura.md).

---

## O SDK

```bash
sudo apt install dotnet-sdk-10.0     # no Ubuntu 26.04 o .NET 10 vem do próprio Ubuntu
dotnet --version                     # 10.0.1xx ou mais novo
```

O `global.json` pede o SDK 10: com só outra versão instalada, o `dotnet build` recusa e diz qual falta.

## Compilar e testar

```bash
dotnet build
dotnet test
```

O `dotnet test` roda pelo Microsoft.Testing.Platform, ligado no `global.json`.

## Um servidor para conversar

O agente precisa do SentinelWeb de pé. No repositório dele:

```bash
bun run dev              # a API sobe na 3001
```

O endereço é `ws://localhost:3001/agent-hub`. O segredo de instalação é o `AGENT_ENROLL_SECRET` do `.env` do
SentinelWeb; vazio, em desenvolvimento o servidor aceita qualquer um. O agente o troca pelo token da máquina
no primeiro contato e apaga o arquivo.

## Rodar o agente

Com sudo: o serial e o UUID do firmware são só do root, e sem eles o agente não tem HWID. Em desenvolvimento,
a linha de comando faz o papel do `/etc/sentinel-agent/agent.json`:

```bash
echo "<AGENT_ENROLL_SECRET>" > /tmp/sentinel-secret && chmod 600 /tmp/sentinel-secret
sudo dotnet run --project SentinelAgente -- \
  --Server:Url=ws://localhost:3001/agent-hub \
  --Server:EnrollSecretFile=/tmp/sentinel-secret \
  --Server:TokenFile=/tmp/sentinel-token
```

O primeiro contato troca o segredo pelo token e apaga o arquivo do segredo; dali em diante, basta o
`--Server:TokenFile`. O HWID é sorteado na primeira vez e fica em `/var/lib/sentinel-agent/hwid`, também na
máquina de desenvolvimento: apagá-lo dá uma máquina nova no SentinelWeb. Instalado pelo pacote, o agente lê tudo do `agent.json`.
