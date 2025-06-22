using ExileCore.Shared;
using System.Text.RegularExpressions;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;
using System.Net;
using System.Collections.Generic;
using System.Globalization;

namespace TujenMem;

public class PoepricesApiResponse
{
  [JsonProperty("currency")]
  public string Currency { get; set; }

  [JsonProperty("error")]
  public int Error { get; set; }

  [JsonProperty("error_msg")]
  public string ErrorMsg { get; set; }

  [JsonProperty("warning_msg")]
  public string WarningMsg { get; set; }

  [JsonProperty("max")]
  public float Max { get; set; }

  [JsonProperty("min")]
  public float Min { get; set; }

  [JsonProperty("pred_confidence_score")]
  public float PredConfidenceScore { get; set; }

  [JsonProperty("pred_explanation")]
  public object[][] PredExplanation { get; set; }
}

public class HaggleItemAbyssJewel : HaggleItem
{
  private static DateTime _lastRequestTime = DateTime.MinValue;

  public int ItemLevel { get; set; }
  public string ItemText { get; set; }
  public int PriceEstimationInChaos { get; set; }
  public int PriceEstimationConfidence { get; set; }
  public bool IsLoadingPrice { get; set; } = false;

  public async SyncTask<bool> GetPriceEstimation()
  {
    try
    {
      Log.Debug($"Starting price estimation for jewel: {Name}");
      IsLoadingPrice = true;

      // Pause stuck detection before HTTP request
      TujenMem.Instance.Scheduler.PauseStuckDetection();

      // Rate limiting: wait if less than 1 second since last request
      var timeSinceLastRequest = DateTime.Now - _lastRequestTime;
      if (timeSinceLastRequest.TotalSeconds < 1)
      {
        var waitTime = (int)((1 - timeSinceLastRequest.TotalSeconds) * 1000);
        Log.Debug($"Rate limiting: waiting {waitTime}ms before API call");
        await InputAsync.Wait(waitTime);
      }
      _lastRequestTime = DateTime.Now;

      var base64ItemText = GetItemText();
      var league = TujenMem.Instance.Settings.League.Value;
      var url = $"https://www.poeprices.info/api?i={base64ItemText}&l={league}&s=awakened-poe-trade";

      Log.Debug($"Calling poeprices API for league: {league}");

      using (var client = new HttpClient())
      {
        client.Timeout = TimeSpan.FromSeconds(5);
        var response = await client.GetStringAsync(url);
        Log.Debug($"Response: {response}");
        var apiResponse = JsonConvert.DeserializeObject<PoepricesApiResponse>(response);

        if (apiResponse.Error != 0)
        {
          Log.Error($"Poeprices API error: {apiResponse.ErrorMsg}");
          // Resume stuck detection before returning
          TujenMem.Instance.Scheduler.ResumeStuckDetection();
          IsLoadingPrice = false;
          return false;
        }

        Log.Debug($"API response: {apiResponse.Currency} {apiResponse.Min}-{apiResponse.Max}, confidence: {apiResponse.PredConfidenceScore:P0}");

        // Convert to chaos if needed
        float priceInChaos = apiResponse.Min; // Use min price as conservative estimate
        if (apiResponse.Currency != "chaos")
        {
          Log.Debug($"Converting from {apiResponse.Currency} to chaos");
          if (apiResponse.Currency == "divine")
          {
            // Get divine to chaos conversion rate from Ninja
            Ninja.Items.TryGetValue("Divine Orb", out var divine);
            var divinePrice = divine?.First().ChaosValue ?? 230f;
            priceInChaos = apiResponse.Min * divinePrice;
            Log.Debug($"Divine conversion rate: {divinePrice}c, converted price: {priceInChaos:F0}c");
          }
          else if (apiResponse.Currency == "exalt")
          {
            // Get exalt to chaos conversion rate from Ninja
            Ninja.Items.TryGetValue("Exalted Orb", out var exalt);
            var exaltPrice = exalt?.First().ChaosValue ?? 0f;
            if (exaltPrice > 0)
            {
              priceInChaos = apiResponse.Min * exaltPrice;
              Log.Debug($"Exalt conversion rate: {exaltPrice}c, converted price: {priceInChaos:F0}c");
            }
            else
            {
              Log.Error("Could not find Exalted Orb price in Ninja data");
              // Resume stuck detection before returning
              TujenMem.Instance.Scheduler.ResumeStuckDetection();
              IsLoadingPrice = false;
              return false;
            }
          }
          else
          {
            Log.Error($"Unknown currency from poeprices API: {apiResponse.Currency}");
            // Resume stuck detection before returning
            TujenMem.Instance.Scheduler.ResumeStuckDetection();
            IsLoadingPrice = false;
            return false;
          }
        }

        PriceEstimationInChaos = (int)priceInChaos;
        PriceEstimationConfidence = (int)apiResponse.PredConfidenceScore;

        Log.Debug($"Price estimation complete: {PriceEstimationInChaos}c (confidence: {PriceEstimationConfidence}%)");

        // Resume stuck detection after successful completion
        TujenMem.Instance.Scheduler.ResumeStuckDetection();
        IsLoadingPrice = false;
        return true;
      }
    }
    catch (Exception ex)
    {
      Log.Error($"Error getting price estimation: {ex.Message}");
      // Resume stuck detection in case of exception
      TujenMem.Instance.Scheduler.ResumeStuckDetection();
      IsLoadingPrice = false;
      return false;
    }
  }

  public async SyncTask<bool> GetPriceEstimationFromWebsite()
  {
    try
    {
      Log.Debug($"Starting price estimation from website for jewel: {Name}");
      IsLoadingPrice = true;

      // Pause stuck detection before HTTP request
      TujenMem.Instance.Scheduler.PauseStuckDetection();

      // Rate limiting
      var timeSinceLastRequest = DateTime.Now - _lastRequestTime;
      if (timeSinceLastRequest.TotalSeconds < 1)
      {
        var waitTime = (int)((1 - timeSinceLastRequest.TotalSeconds) * 1000);
        Log.Debug($"Rate limiting: waiting {waitTime}ms before web request");
        await InputAsync.Wait(waitTime);
      }
      _lastRequestTime = DateTime.Now;

      var league = TujenMem.Instance.Settings.League.Value;
      var url = "https://www.poeprices.info/query";

      Log.Debug($"Calling poeprices website for league: {league}");

      using (var client = new HttpClient())
      {
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
        client.DefaultRequestHeaders.Add("Accept-Language", "de;q=0.5");
        client.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
        client.DefaultRequestHeaders.Add("Origin", "https://www.poeprices.info");
        client.DefaultRequestHeaders.Add("Referer", "https://www.poeprices.info/");
        client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
        client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
        client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
        client.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
        client.DefaultRequestHeaders.Add("Sec-GPC", "1");
        client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua", @"""Brave"";v=""137"", ""Chromium"";v=""137"", ""Not/A)Brand"";v=""24""");
        client.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
        client.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-platform", @"""Windows""");

        var rawItemText = GetRawItemText();
        var formData = new FormUrlEncodedContent(new[]
        {
          new KeyValuePair<string, string>("itemtext", rawItemText),
          new KeyValuePair<string, string>("league", league),
          new KeyValuePair<string, string>("auto", "auto"),
          new KeyValuePair<string, string>("submit", "Submit"),
          new KeyValuePair<string, string>("myshops", ""),
          new KeyValuePair<string, string>("myaccounts", ""),
        });

        var response = await client.PostAsync(url, formData);
        var html = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
          Log.Error($"Poeprices website request failed with status code {response.StatusCode}");
          // Resume stuck detection before returning
          TujenMem.Instance.Scheduler.ResumeStuckDetection();
          IsLoadingPrice = false;
          return false;
        }

        var pattern = @"<tr class=""price_tr"">\s*<td class=""price_highlight"">([\d\.]+) ~ ([\d\.]+)</td>\s*<td class=""price_highlight"">(\w+)</td>";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var usedSpanPattern = false;

        if (!match.Success)
        {
          // Fallback: try to find price in span element when table is not present
          Log.Debug("Table price pattern not found, trying span fallback pattern");
          var spanPattern = @"<span class=""price_highlight"">([\d\.]+)\s+(\w+)</span>";
          match = Regex.Match(html, spanPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
          usedSpanPattern = true;

          if (!match.Success)
          {
            Log.Error("Could not find price in poeprices.info HTML response (neither table nor span pattern matched).");
            // Resume stuck detection before returning
            TujenMem.Instance.Scheduler.ResumeStuckDetection();
            IsLoadingPrice = false;
            return false;
          }

          Log.Debug("Found price using span fallback pattern");
        }

        var minPriceStr = match.Groups[1].Value;
        float minPrice;
        if (!float.TryParse(minPriceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out minPrice))
        {
          Log.Error($"Could not parse min price from string: {minPriceStr}");
          // Resume stuck detection before returning
          TujenMem.Instance.Scheduler.ResumeStuckDetection();
          IsLoadingPrice = false;
          return false;
        }

        // Currency is in group 3 for table pattern, group 2 for span pattern
        var currencyGroupIndex = usedSpanPattern ? 2 : 3;
        var currency = match.Groups[currencyGroupIndex].Value.ToLower();

        Log.Debug($"Parsed from website: {minPrice} {currency}");

        float priceInChaos = minPrice;
        if (currency != "chaos")
        {
          Log.Debug($"Converting from {currency} to chaos");
          if (currency == "divine")
          {
            Ninja.Items.TryGetValue("Divine Orb", out var divine);
            var divinePrice = divine?.First().ChaosValue ?? 230f;
            priceInChaos = minPrice * divinePrice;
            Log.Debug($"Divine conversion rate: {divinePrice}c, converted price: {priceInChaos:F0}c");
          }
          else if (currency == "exalt")
          {
            Ninja.Items.TryGetValue("Exalted Orb", out var exalt);
            var exaltPrice = exalt?.First().ChaosValue ?? 0f;
            if (exaltPrice > 0)
            {
              priceInChaos = minPrice * exaltPrice;
              Log.Debug($"Exalt conversion rate: {exaltPrice}c, converted price: {priceInChaos:F0}c");
            }
            else
            {
              Log.Error("Could not find Exalted Orb price in Ninja data");
              // Resume stuck detection before returning
              TujenMem.Instance.Scheduler.ResumeStuckDetection();
              IsLoadingPrice = false;
              return false;
            }
          }
          else
          {
            Log.Error($"Unknown currency from poeprices website: {currency}");
            // Resume stuck detection before returning
            TujenMem.Instance.Scheduler.ResumeStuckDetection();
            IsLoadingPrice = false;
            return false;
          }
        }

        PriceEstimationInChaos = (int)priceInChaos;
        PriceEstimationConfidence = 100;

        Log.Debug($"Price estimation from website complete: {PriceEstimationInChaos}c");

        // Resume stuck detection after successful completion
        TujenMem.Instance.Scheduler.ResumeStuckDetection();
        IsLoadingPrice = false;
        return true;
      }
    }
    catch (Exception ex)
    {
      Log.Error($"Error getting price estimation from website: {ex.Message}");
      // Resume stuck detection in case of exception
      TujenMem.Instance.Scheduler.ResumeStuckDetection();
      IsLoadingPrice = false;
      return false;
    }
  }

  private string GetRawItemText()
  {
    var text = Regex.Replace(ItemText, @"(?<=\d)(\([^)]+\))", "");
    text = Regex.Replace(text, @"^\{.+\}\r?\n?", "", RegexOptions.Multiline);
    return text;
  }

  private string GetItemText()
  {
    var text = Regex.Replace(ItemText, @"(?<=\d)(\([^)]+\))", "");
    text = Regex.Replace(text, @"^\{.+\}\r?\n?", "", RegexOptions.Multiline);
    var bytes = System.Text.Encoding.UTF8.GetBytes(text);
    return Convert.ToBase64String(bytes);
  }
}