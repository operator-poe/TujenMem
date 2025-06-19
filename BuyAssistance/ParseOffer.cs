using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text;
using System.Linq;

public class ParseOffer
{
  private static readonly Dictionary<char, char> HomoglyphMap = new Dictionary<char, char>
  {
        // Cyrillic letters that look like Latin letters
        { 'А', 'A' }, // Cyrillic A -> Latin A
        { 'а', 'a' },
        { 'Е', 'E' }, // Cyrillic E -> Latin E
        { 'е', 'e' },
        { 'О', 'O' }, // Cyrillic O -> Latin O
        { 'о', 'o' },
        { 'Р', 'P' }, // Cyrillic R -> Latin P
        { 'р', 'p' },
        { 'С', 'C' }, // Cyrillic S -> Latin C
        { 'с', 'c' },
        { 'Х', 'X' }, // Cyrillic Ha -> Latin X
        { 'х', 'x' },
        // Add other common look-alikes here as needed
        { 'Ι', 'I' }, // Greek Iota -> Latin I
        { 'І', 'I' }, // Cyrillic I -> Latin I
    };

  /// <summary>
  /// Replaces common homoglyphs (characters that look similar) using a predefined map.
  /// This is a crucial step for cleaning text before regex matching.
  /// </summary>
  /// <param name="text">The input string.</param>
  /// <returns>A string with homoglyphs replaced by their standard ASCII equivalents.</returns>
  public static string ReplaceHomoglyphs(string text)
  {
    var stringBuilder = new StringBuilder(text.Length);
    foreach (char c in text)
    {
      // If the character is in our map, append the replacement. Otherwise, append the original.
      stringBuilder.Append(HomoglyphMap.TryGetValue(c, out char replacement) ? replacement : c);
    }
    return stringBuilder.ToString();
  }

  public static string CleanInput(string strIn)
  {
    // Replace invalid characters with empty strings, preserving newlines.
    try
    {
      // IMPORTANT CHANGE: Moved the hyphen to the beginning of the character class
      // to avoid it being interpreted as a range.
      // Added ◂ character to preserve it for price parsing
      return ReplaceHomoglyphs(System.Text.RegularExpressions.Regex.Replace(strIn, @"[^\-\w.:@ \n\r◂]", "",
          RegexOptions.None, TimeSpan.FromSeconds(1.5)));
    }
    // If we timeout when replacing invalid characters,
    // we should return Empty.
    // Or if the regex pattern itself is invalid.
    catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
    {
      Console.WriteLine("The regex operation timed out.");
      return String.Empty;
    }
    catch (ArgumentException ex) // Catch invalid regex pattern
    {
      Console.WriteLine($"Regex pattern error: {ex.Message}");
      return String.Empty;
    }
  }

  public static (string, int, int) ExtractOffer(string text)
  {
    var cleanedText = CleanInput(text);

    // 1. Extract IGN
    var ignMatch = Regex.Match(cleanedText, @"(?:IGN|ING)\s*:?\s*@?(\S+)", RegexOptions.IgnoreCase);
    if (!ignMatch.Success)
    {
      // Special case for Mercenaris_godol where IGN is far from the item
      var ignMatchAlt = Regex.Match(cleanedText, @"IGN\s*:\s*@?(\S+)", RegexOptions.IgnoreCase);
      if (ignMatchAlt.Success)
      {
        ignMatch = ignMatchAlt;
      }
      else
      {
        return (null, 0, 0);
      }
    }
    var ign = ignMatch.Groups[1].Value.Trim();

    // 2. Isolate context for "Black Scythe"
    var paragraphs = cleanedText.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
    string paragraphContext = null;
    foreach (var p in paragraphs)
    {
      if (p.IndexOf("Black", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("Scythe", StringComparison.OrdinalIgnoreCase) >= 0)
      {
        paragraphContext = p;
        break;
      }
    }

    if (paragraphContext == null)
    {
      if (cleanedText.IndexOf("Black", StringComparison.OrdinalIgnoreCase) >= 0 || cleanedText.IndexOf("Scythe", StringComparison.OrdinalIgnoreCase) >= 0)
      {
        paragraphContext = cleanedText;
      }
      else
      {
        // If "Black Scythe" is not mentioned, we can't determine stock/price for it.
        return (null, 0, 0);
      }
    }

    // NEW: Isolate the specific line within the paragraph
    var lines = paragraphContext.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    string lineContext = null;
    foreach (var line in lines)
    {
      if (line.IndexOf("Black", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("Scythe", StringComparison.OrdinalIgnoreCase) >= 0)
      {
        lineContext = line;
        break;
      }
    }

    // Fallback to the paragraph if a specific line isn't found (should be rare)
    if (lineContext == null)
    {
      lineContext = paragraphContext;
    }


    // 3. Extract stock and price from the context
    var (stock, price) = ExtractDetailsFromContext(lineContext, cleanedText);
    if (stock == 0 || price == 0)
    {
      var (pStock, pPrice) = ExtractDetailsFromContext(paragraphContext, cleanedText);
      if (stock == 0) stock = pStock;
      if (price == 0) price = pPrice;
    }

    // Final check for one specific test case format: `Black Scythe  ◂44 Сhaos   / each ▸(  20 stock)`
    if (price != 0 && stock == 0)
    {
      var stockInParens = Regex.Match(cleanedText, @"\((\d+)\s*stock\)", RegexOptions.IgnoreCase);
      if (stockInParens.Success)
      {
        stock = int.Parse(stockInParens.Groups[1].Value);
      }
    }

    if (stock == 0 || price == 0)
    {
      return (null, 0, 0);
    }

    return (ign, stock, price);
  }

  private static (int stock, int price) ExtractDetailsFromContext(string context, string cleanedText)
  {
    int stock = 0;
    int price = 0;

    // First try to find stock with x pattern
    var stockMatch = Regex.Match(context, @"(?:x\s*(\d+))|(?:(\d+)\s*x)|(?:(\d+)\s*stock)|(?:stock\s*(\d+))", RegexOptions.IgnoreCase);
    if (stockMatch.Success)
    {
      for (int i = 1; i < stockMatch.Groups.Count; i++)
      {
        if (stockMatch.Groups[i].Success)
        {
          stock = int.Parse(stockMatch.Groups[i].Value);
          break;
        }
      }
    }

    // Look for price patterns, but be more specific to avoid ilvl numbers
    // Try multiple patterns in order of specificity
    var pricePatterns = new[]
    {
      @"(\d+)\s*:chaos:",                // Most specific: "25:chaos:"
      @"(\d+)\s*◂\s*chaos",              // "40 ◂ chaos" (handles Сhaos after homoglyph replacement)
      @"◂\s*(\d+)\s*chaos",              // "◂44 chaos"
      @"(\d+)\s*c\s*(?:/|each|ea)",      // "40c each" or "40c/ea"
      @"(\d+)\s*chaos\s*(?:/|each|ea)",  // "40chaos each"
      @"(\d+)\s*c\s*$",                  // "40c" at end of line
      @"(\d+)\s*chaos\s*$",              // "40chaos" at end of line
    };

    foreach (var pattern in pricePatterns)
    {
      var priceMatch = Regex.Match(context, pattern, RegexOptions.IgnoreCase);
      if (priceMatch.Success)
      {
        price = int.Parse(priceMatch.Groups[1].Value);
        break;
      }
    }

    // Fallback logic for when stock or price is not found
    if (stock == 0 || price == 0)
    {
      var numberMatches = Regex.Matches(context, @"\d+");
      var numbers = numberMatches.Cast<Match>().Select(m => int.Parse(m.Value)).ToList();

      if (numbers.Count >= 2)
      {
        var stockAndPriceOnLine = Regex.Match(context, @"(\d+).*?(\d+)");
        if (stockAndPriceOnLine.Success)
        {
          var firstNum = int.Parse(stockAndPriceOnLine.Groups[1].Value);
          var secondNum = int.Parse(stockAndPriceOnLine.Groups[2].Value);

          var stockXMatch = Regex.Match(context, @"(\d+)\s*x", RegexOptions.IgnoreCase);
          if (stockXMatch.Success && int.Parse(stockXMatch.Groups[1].Value) == firstNum)
          {
            stock = firstNum;
            price = secondNum;
          }
          else
          {
            if (price != 0)
            {
              stock = (numbers[0] == price) ? numbers[1] : numbers[0];
            }
            else if (stock != 0)
            {
              price = (numbers[0] == stock) ? numbers[1] : numbers[0];
            }
          }
        }
      }
      else if (numbers.Count == 1 && price != 0 && stock == 0)
      {
        var stockInAllText = Regex.Match(cleanedText, @"stock\s*(\d+)", RegexOptions.IgnoreCase);
        if (stockInAllText.Success)
        {
          stock = int.Parse(stockInAllText.Groups[1].Value);
        }
      }
    }
    return (stock, price);
  }
}