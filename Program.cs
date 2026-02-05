using IpParser;
using Newtonsoft.Json;

string filePath = "IPs.txt";
List<string> ips = await FileService.LoadIps(filePath);
if (ips.Count == 0)
{
    Console.WriteLine("Нет IP-адресов для обработки");
    return;
}
Console.WriteLine($"Загружено {ips.Count} IP-адресов\n");
List<IpData> ipDataList = await IpService.LoadAllIpDataAsync(ips);
if (ipDataList.Count == 0)
{
    Console.WriteLine("По всем загруженным IP нет данных");
    return;
}
StatisticsService.PrintCountryStatistics(ipDataList);

namespace IpParser
{
    public class IpData
    {
        [JsonProperty("city")]
        public string? City { get; set; }

        [JsonProperty("country")]
        public string? Country { get; set; }
    }

    public static class FileService
    {
        public static async Task<List<string>>LoadIps(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine("Файл не найден: " + filePath);
                return [];
            }
            var lines = await File.ReadAllLinesAsync(filePath);

            return [.. lines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim())];
        }
    }

    public static class IpService
    {
        private static readonly HttpClient _httpClient = new();
        private static readonly SemaphoreSlim _semaphore = new(5);

        public static async Task<IpData?> GetIpDataAsync(string ip)
        {
            await _semaphore.WaitAsync();
            try
            {
                string url = $"https://ipinfo.io/{ip}/json";
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Ошибка API для {ip}: {response.StatusCode}");
                    return null;
                }
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<IpData>(json);
                return data;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при запросе по IP {ip}: {ex.Message}");
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public static async Task<List<IpData>> LoadAllIpDataAsync(List<string> ips)
        {
            var tasks = ips.Select(GetIpDataAsync);
            var results = await Task.WhenAll(tasks);
            return results.Where(x => x != null).ToList()!;
        }
    }

    public static class StatisticsService
    {
        public static void PrintCountryStatistics(List<IpData> ipDataList)
        {
            var countryStats = ipDataList
                .Where(x => !string.IsNullOrEmpty(x.Country))
                .GroupBy(x => x.Country)
                .Select(g => new
                {
                    Country = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            if (countryStats.Count == 0)
            {
                Console.WriteLine("Для всех загруженных IP не удалось определить страны");
                return;
            }
            Console.WriteLine("Статистика по странам:");
            foreach (var item in countryStats)
            {
                Console.WriteLine($"{item.Country} - {item.Count}");
            }

            var topCountry = countryStats.First().Country;
            Console.WriteLine($"\nГорода страны {topCountry}:");
            var cities = ipDataList
                .Where(x => x.Country == topCountry && !string.IsNullOrEmpty(x.City))
                .Select(x => x.City)
                .Distinct();
            foreach (var city in cities)
            {
                Console.WriteLine(city);
            }
        }
    }
}