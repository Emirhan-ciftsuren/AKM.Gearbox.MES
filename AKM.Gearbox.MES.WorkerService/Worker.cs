using System.Xml.Linq;
using Microsoft.Data.SqlClient;

namespace AKM.Gearbox.MES.WorkerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Klasör yollarýný ve baðlantý cümlesini güvenli bir þekilde tanýmlýyoruz
            string inputPath = @"C:\MES_Files\Input";
            string successedPath = @"C:\MES_Files\Successed";
            string failedPath = @"C:\MES_Files\Failed";
            string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "Server=localhost;Database=SirketAdi_Gearbox_MES;Trusted_Connection=True;TrustServerCertificate=True;";

            // Klasör kontrollerini üretim garantisi için tutuyoruz
            if (!Directory.Exists(inputPath)) Directory.CreateDirectory(inputPath);
            if (!Directory.Exists(successedPath)) Directory.CreateDirectory(successedPath);
            if (!Directory.Exists(failedPath)) Directory.CreateDirectory(failedPath);

            _logger.LogInformation("Worker Service started monitoring: {path}", inputPath);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Klasördeki sadece .xml uzantýlý dosyalarý getir 
                    var xmlFiles = Directory.GetFiles(inputPath, "*.xml");

                    foreach (var file in xmlFiles)
                    {
                        _logger.LogInformation("New XML file found: {file}", Path.GetFileName(file));

                        bool isSuccess = ProcessXmlFile(file, connectionString);
                        string fileName = Path.GetFileName(file);

                        if (isSuccess)
                        {
                            File.Move(file, Path.Combine(successedPath, fileName), overwrite: true);
                            _logger.LogInformation("File moved to SUCCESSED folder.");
                        }
                        else
                        {
                            File.Move(file, Path.Combine(failedPath, fileName), overwrite: true);
                            _logger.LogError("File moved to FAILED folder due to an error.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during directory polling.");
                }

                // Üretim standartlarýnda iþlemciyi yormamak için 10 saniye bekleme süresi
                await Task.Delay(10000, stoppingToken);
            }
        }

        private bool ProcessXmlFile(string filePath, string connectionString)
        {
            try
            {
                // XML dosyasýný yükle ve oku
                XDocument doc = XDocument.Load(filePath);

                // XML elementlerini güvenli bir þekilde deðiþkenlere alýyoruz
                string orderNo = doc.Root?.Element("OrderNo")?.Value ?? "";
                string orderDesc = doc.Root?.Element("OrderDesc")?.Value ?? "";
                string orderType = doc.Root?.Element("OrderType")?.Value ?? "Standard";
                string createdBy = doc.Root?.Element("CreatedBy")?.Value ?? "System";
                int quantity = int.Parse(doc.Root?.Element("OrderQuantity")?.Value ?? "1");

                if (string.IsNullOrEmpty(orderNo))
                {
                    _logger.LogWarning("Invalid XML: OrderNo is missing.");
                    return false;
                }

                // SQL Baðlantýsýný açýyoruz
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // CRITICAL USE CASE: Sipariþi alt parçalara (Quantity kadar) bölerek kaydediyoruz
                    for (int i = 1; i <= quantity; i++)
                    {
                        // Örn: 1000.1, 1000.2 formatýný oluþturuyoruz
                        string splittedOrderId = $"{orderNo}.{i}";

                        string query = @"INSERT INTO Orders 
                                         (OrderId, OrderDesc, OrderCreationDate, OrderType, OrderStatus, CreatedBY, IsProcessed) 
                                         VALUES 
                                         (@OrderId, @OrderDesc, GETDATE(), @OrderType, 'Pending', @CreatedBy, 0)";

                        using (SqlCommand command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@OrderId", splittedOrderId);
                            command.Parameters.AddWithValue("@OrderDesc", $"{orderDesc} (Part {i}/{quantity})");
                            command.Parameters.AddWithValue("@OrderType", orderType);
                            command.Parameters.AddWithValue("@CreatedBy", createdBy);

                            command.ExecuteNonQuery();
                        }

                        _logger.LogInformation("Saved to database: {orderId}", splittedOrderId);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing XML file: {file}", filePath);
                return false;
            }
        }
    }
}