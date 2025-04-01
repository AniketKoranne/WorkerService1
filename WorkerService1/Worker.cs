using System;
using System.Data.SqlClient;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace WorkerService1
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly string _connectionString;
        private readonly string _uploadApiUrl;
        private readonly string _downloadApiUrl;
        private readonly int _checkIntervalMinutes;
        private readonly int _delayBetweenApiCallsMinutes;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClient = new HttpClient();
            _connectionString = _configuration.GetConnectionString("DefaultConnection");
            _uploadApiUrl = _configuration["ApiUrls:Upload"];
            _downloadApiUrl = _configuration["ApiUrls:Download"];
            _checkIntervalMinutes = int.Parse(_configuration["WorkerSettings:CheckIntervalMinutes"]);
            _delayBetweenApiCallsMinutes = int.Parse(_configuration["WorkerSettings:DelayBetweenApiCallsMinutes"]);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Employee API Worker Service Started at: {time}", DateTimeOffset.Now);

            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessPendingEmployees();
                await Task.Delay(TimeSpan.FromMinutes(_checkIntervalMinutes), stoppingToken);
            }
        }

        private async Task ProcessPendingEmployees()
        {
            using SqlConnection conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string query = "SELECT ID, EmpCode FROM APIQueue WHERE Status = 'Pending'";
            using SqlCommand cmd = new SqlCommand(query, conn);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                int id = reader.GetInt32(0);
                string empCode = reader.GetString(1);

                _logger.LogInformation("Processing EmpCode: {empCode}", empCode);

                await SendApiRequest(_uploadApiUrl, empCode);
                await UpdateQueueStatus(id, "UploadSent", conn);

                _logger.LogInformation("Waiting {minutes} minutes before next API call...", _delayBetweenApiCallsMinutes);
                await Task.Delay(TimeSpan.FromMinutes(_delayBetweenApiCallsMinutes));

                await SendApiRequest(_downloadApiUrl, empCode);
                await UpdateQueueStatus(id, "DownloadSent", conn);

                _logger.LogInformation("Waiting {minutes} minutes before final API call...", _delayBetweenApiCallsMinutes);
                await Task.Delay(TimeSpan.FromMinutes(_delayBetweenApiCallsMinutes));

                await SendApiRequest(_uploadApiUrl, empCode);
                await UpdateQueueStatus(id, "Completed", conn);

                _logger.LogInformation("Processing Completed for EmpCode: {empCode}", empCode);
            }
        }

        private async Task SendApiRequest(string apiUrl, string empCode)
        {
            var payload = new
            {
                Action = apiUrl.Contains("upload") ? "UploadUser" : "DownloadUser",
                AutoOTP = "0",
                EmpCode = empCode,
                DeviceSN = "PEEPL20230248018",
                RFID = "1",
                Face = "0",
                FP = "0",
                FPID = "0"
            };

            string jsonBody = JsonConvert.SerializeObject(payload);

            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync(apiUrl, content);
            string responseString = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("API Response for {empCode}: {response}", empCode, responseString);
        }

        private async Task UpdateQueueStatus(int id, string status, SqlConnection conn)
        {
            using SqlCommand cmd = new SqlCommand("UPDATE APIQueue SET Status = @Status WHERE ID = @ID", conn);
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@ID", id);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
