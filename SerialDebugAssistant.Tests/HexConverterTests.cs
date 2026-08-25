using System;
using SerialDebugAssistant.Utils;
using Xunit;

namespace SerialDebugAssistant.Tests;

public class HexConverterTests
{
    [Theory]
    [InlineData("", new byte[0])]
    [InlineData("41", new byte[] { 0x41 })]
    [InlineData("41 42", new byte[] { 0x41, 0x42 })]
    [InlineData("4142", new byte[] { 0x41, 0x42 })]
    [InlineData("0x41 0x42", new byte[] { 0x41, 0x42 })]
    public void HexStringToBytes_ValidInput_ReturnsBytes(string input, byte[] expected)
    {
        var result = HexConverter.HexStringToBytes(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("4")]
    [InlineData("4G")]
    [InlineData("412")]
    public void HexStringToBytes_InvalidInput_ThrowsFormatException(string input)
    {
        Assert.Throws<FormatException>(() => HexConverter.HexStringToBytes(input));
    }

    [Theory]
    [InlineData(new byte[] { 0x41, 0x42 }, "41 42")]
    [InlineData(new byte[0], "")]
    public void BytesToHexString_Converts(byte[] input, string expected)
    {
        Assert.Equal(expected, HexConverter.BytesToHexString(input));
    }

    [Theory]
    [InlineData("AB", new byte[] { 0x41, 0x42 })]
    public void AsciiToBytes_Converts(string input, byte[] expected)
    {
        Assert.Equal(expected, HexConverter.AsciiToBytes(input));
    }

    [Theory]
    [InlineData(new byte[] { 0x41, 0x42 }, "AB")]
    public void BytesToAscii_Converts(byte[] input, string expected)
    {
        Assert.Equal(expected, HexConverter.BytesToAscii(input));
    }

    [Fact]
    public void HexStringToBytes_Null_ThrowsArgumentNullException()
        => Assert.Throws<ArgumentNullException>(() => HexConverter.HexStringToBytes(null!));

    [Fact]
    public void AsciiToBytes_Null_ThrowsArgumentNullException()
        => Assert.Throws<ArgumentNullException>(() => HexConverter.AsciiToBytes(null!));

    [Fact]
    public void BytesToHexString_Null_ThrowsArgumentNullException()
        => Assert.Throws<ArgumentNullException>(() => HexConverter.BytesToHexString(null!));

    [Fact]
    public void BytesToAscii_Null_ThrowsArgumentNullException()
        => Assert.Throws<ArgumentNullException>(() => HexConverter.BytesToAscii(null!));
}
