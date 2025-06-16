using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text;

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

  protected static string CleanInput(string strIn)
  {
    // Replace invalid characters with empty strings, preserving newlines.
    try
    {
      // IMPORTANT CHANGE: Moved the hyphen to the beginning of the character class
      // to avoid it being interpreted as a range.
      return ReplaceHomoglyphs(System.Text.RegularExpressions.Regex.Replace(strIn, @"[^\-\w.@ \n\r]", "",
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
    // TODO: Extract by using tests
    return (null, 0, 0);
  }
}