using System.Drawing;
using AssettoServer.Network.ClientMessages;
// ReSharper disable InconsistentNaming

namespace AssettoServer.Tests;

// Based on https://github.com/ac-custom-shaders-patch/acc-lua-sdk/blob/ca9530fbb5c81d0c23c4c1ba7a8f198870d2b2a3/tests/test_struct_item.lua
public class OnlineEventGeneratorTests
{
    [Test]
    public void Test_Minimal()
    {
        var testMessage1 = OnlineEventGenerator.ParseClientMessage(typeof(TestMessage1));
        Assert.That(OnlineEventGenerator.GenerateStructure(testMessage1.Key, testMessage1.Fields), Is.EqualTo("int i00;int i01;"));
        
        var testMessage2 = OnlineEventGenerator.ParseClientMessage(typeof(TestMessage2));
        Assert.That(OnlineEventGenerator.GenerateStructure(testMessage2.Key, testMessage2.Fields), Is.EqualTo("double i10;int i11;"));

        var testMessage3 = OnlineEventGenerator.ParseClientMessage(typeof(TestMessage3));
        Assert.That(OnlineEventGenerator.GenerateStructure(testMessage3.Key, testMessage3.Fields), Is.EqualTo("double i23;int i22;uint8_t i21;char i20[20];"));

        var testMessage4 = OnlineEventGenerator.ParseClientMessage(typeof(TestMessage4));
        Assert.That(OnlineEventGenerator.GenerateStructure(testMessage4.Key, testMessage4.Fields), Is.EqualTo("double i23;int i22[4];uint8_t i21;char i20[20];"));
        
        var testMessage5 = OnlineEventGenerator.ParseClientMessage(typeof(TestMessage5));
        Assert.That(OnlineEventGenerator.GenerateStructure(testMessage5.Key, testMessage5.Fields), Is.EqualTo("rgbm i51;float i50;"));
    }
}

public class TestMessage1
{
    [OnlineEventField(Name = "i00")]
    public int i00;
    [OnlineEventField(Name = "i01")]
    public int i01;
}

public class TestMessage2
{
    [OnlineEventField(Name = "i10")]
    public double i10;
    [OnlineEventField(Name = "i11")]
    public int i11;
}

public class TestMessage3
{
    [OnlineEventField(Name = "i20", Size = 20)]
    public string i20 = null!;
    [OnlineEventField(Name = "i21")]
    public byte i21;
    [OnlineEventField(Name = "i22")]
    public int i22;
    [OnlineEventField(Name = "i23")]
    public double i23;
}

public class TestMessage4
{
    [OnlineEventField(Name = "i20", Size = 20)]
    public string i20 = null!;
    [OnlineEventField(Name = "i21")]
    public byte i21;
    [OnlineEventField(Name = "i22", Size = 4)]
    public int[] i22 = null!;
    [OnlineEventField(Name = "i23")]
    public double i23;
}

public class TestMessage5
{
    [OnlineEventField(Name = "i50")]
    public float i50;
    [OnlineEventField(Name = "i51")]
    public Color i51;
}
