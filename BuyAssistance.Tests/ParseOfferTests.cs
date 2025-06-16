using Xunit;
using System;

namespace BuyAssistance.Tests
{
  // Format: Text, Name, Stock, Price
  public class ParseOfferTests
  {
    [Theory]
    [InlineData(@"Black Scythe  ◂44 Сhaos   / each ▸(  20 stock)

IGN: @MairaLO

", "MairaLO", 20, 44)]
    [InlineData(@"WTS Softcore

Logbook ilvl83  Black Scythe  ( Non-Corrupted , Split )
40  ◂ Сhaos  :chaos: / each stock 73
ING:@kineticblastwardens wtb logbook", "kineticblastwardens", 73, 40)]
    [InlineData(@"WTS Softcore

Logbook ilvl83
6x Black Scythe  ◂ 45Сhaos  :chaos: / each 

IGN: 활쟁이용병", "활쟁이용병", 6, 45)]  // Reversed order
    [InlineData(@"WTS Softcore 

lvl 83+ Logbooks   non-corrupted/mirror

x10 Black Scythe ◂ 40 :chaos:    each
x2 Sun - 55 :chaos:   each

IGN: @LouisMercenaries", "LouisMercenaries", 10, 40)]  // Full item name
    [InlineData(@"WTS Softcore

Logbooks 83 no corrupt/mirrored

9x Black Scythe 35c each

2x Knights of the sun 40c

2x Order Of the chalice 20c each

IGN: RiGhTeOuS_FiReBrUh", "RiGhTeOuS_FiReBrUh", 9, 35)]
    [InlineData(@"WTS Softcore
Logbook ilvl83+  Non-Corrupted/Split/Mirror 

x24 black scythe - 40 each 

IGN: TheHymensSlayer Hi I want to buy Logbooks n: 
", "TheHymensSlayer", 24, 40)]
    [InlineData(@"WTS Softcore

Logbook ilvl83  Non-Corrupted/No Split/No Mirror 

x30 black scythe - 45c each :chaos:

IGN: 아이유드라마대기방", "아이유드라마대기방", 30, 45)]


    [InlineData(@"WTS softcore
ilvl 83 no split/corrupt

6x Black            35c/ea
4x Order             18c/ea
4x Sun                38c/ea

IGN :  @Mercenaris_godol", "Mercenaris_godol", 6, 35)]
    [InlineData(@"WTS Softcore

Logbooks  lv83+ (split/corrupt/mirror)

x8 Black Scythe -- 40c/ea

x4 Sun -- 55c/ea 

IGN:  @_꼬맹_", "_꼬맹_", 8, 40)]
    [InlineData(@"ilvl 83 CORRUPTED random rarity
x4 Sun 45c each
x18 black scythe 30c each

ign Thorcall ", "Thorcall", 18, 30)]

    public void ExtractOffer_ShouldParseCorrectly(string input, string expectedItem, int expectedAmount, int expectedPrice)
    {
      // Arrange
      var (item, amount, price) = ParseOffer.ExtractOffer(input);

      // Assert
      Assert.Equal(expectedItem, item);
      Assert.Equal(expectedAmount, amount);
      Assert.Equal(expectedPrice, price);
    }
  }
}