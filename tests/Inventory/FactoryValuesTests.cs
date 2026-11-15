using SentinelAgente.Inventory;

namespace SentinelAgente.Tests.Inventory;

public sealed class FactoryValuesTests
{
    [Theory]
    [InlineData("To Be Filled By O.E.M.")]
    [InlineData("Default string")]
    [InlineData("System Serial Number")]
    [InlineData("123456789")]
    [InlineData("0123456789")]
    [InlineData("0")]
    [InlineData("N/A")]
    [InlineData("0000000000")]
    [InlineData("xxxxxxxx")]
    [InlineData("")]
    public void Serial_de_fabrica_nao_serve(string serial)
    {
        Assert.False(FactoryValues.IsRealSerial(serial));
    }

    [Theory]
    [InlineData("0000-0000")]
    [InlineData("00 00 00 00")]
    [InlineData("SystemSerialNumber")]
    [InlineData("Base Board Serial Number")]
    [InlineData("A-B-C")]
    public void O_serial_de_fabrica_se_reconhece_sem_os_separadores(string serial)
    {
        Assert.False(FactoryValues.IsRealSerial(serial));
    }

    [Fact]
    public void O_serial_da_placa_com_barras_da_dell_serve()
    {
        Assert.True(FactoryValues.IsRealSerial("/B4C04F4B/CNCMK0004L0123/"));
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")]
    [InlineData("03000200-0400-0500-0006-000700080009")]
    [InlineData("03020100-0504-0706-0809-0a0b0c0d0e0f")]
    [InlineData("10000000-0000-8000-0040-000000000000")]
    [InlineData("00020003-0004-0005-0006-000700080009")]
    [InlineData("00010203-0405-0607-0809-0a0b0c0d0e0f")]
    [InlineData("nao-e-uuid")]
    public void Uuid_de_fabrica_nao_serve(string uuid)
    {
        Assert.False(FactoryValues.IsRealUuid(uuid));
    }
}
