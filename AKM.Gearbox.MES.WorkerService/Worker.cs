using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
            // --- 1. Klasör ve Baðlantý Yapýlandýrmalarý ---

            // ERP -> MES (Sipariþ Giriþ Yollarý)
            string inputPath = _configuration["FolderPaths:Input"] ?? @"C:\MES_Files\Input";
            string successedPath = _configuration["FolderPaths:Successed"] ?? @"C:\MES_Files\Successed";
            string failedPath = _configuration["FolderPaths:Failed"] ?? @"C:\MES_Files\Failed";

            // MES -> ERP (Sipariþ Çýkýþ / Geri Bildirim Yollarý)
            string outputPath = _configuration["FolderPaths:Output"] ?? @"C:\MES_Files\Output";
            string archivePath = _configuration["FolderPaths:Archive"] ?? @"C:\MES_Files\Archive";

            string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "Server=localhost;Database=AKM_Gearbox_MES;Trusted_Connection=True;TrustServerCertificate=True;";

            // Klasörlerin varlýðýndan emin oluyoruz
            if (!Directory.Exists(inputPath)) Directory.CreateDirectory(inputPath);
            if (!Directory.Exists(successedPath)) Directory.CreateDirectory(successedPath);
            if (!Directory.Exists(failedPath)) Directory.CreateDirectory(failedPath);
            if (!Directory.Exists(outputPath)) Directory.CreateDirectory(outputPath);
            if (!Directory.Exists(archivePath)) Directory.CreateDirectory(archivePath);

            _logger.LogInformation("Worker Service aktif. Dinlenen yollar: \nInput: {input}\nOutput: {output}", inputPath, outputPath);

            while (!stoppingToken.IsCancellationRequested)
            {
                // --- AKIÞ A: ERP'den Gelen Yeni Sipariþleri Ýþleme (Mevcut Mantýðýn) ---
                try
                {
                    var xmlFiles = Directory.GetFiles(inputPath, "*.xml");
                    foreach (var file in xmlFiles)
                    {
                        _logger.LogInformation("Yeni Giriþ XML'i tespit edildi: {file}", Path.GetFileName(file));

                        bool isSuccess = ProcessInputXmlFile(file, connectionString);
                        string fileName = Path.GetFileName(file);

                        if (isSuccess)
                        {
                            File.Move(file, Path.Combine(successedPath, fileName), overwrite: true);
                            _logger.LogInformation("Giriþ XML'i SUCCESSED klasörüne taþýndý.");
                        }
                        else
                        {
                            File.Move(file, Path.Combine(failedPath, fileName), overwrite: true);
                            _logger.LogError("Giriþ XML'i hata nedeniyle FAILED klasörüne taþýndý.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Giriþ (Input) klasörü taranýrken bir hata oluþtu.");
                }


                // --- AKIÞ B: Web Operator'den Gelen Tamamlanmýþ XML'leri Ýþleme (Yeni Eklenen) ---
                try
                {
                    var outputFiles = Directory.GetFiles(outputPath, "*.xml");
                    foreach (var file in outputFiles)
                    {
                        _logger.LogInformation("Tamamlanmýþ Çýktý XML'i tespit edildi: {file}", Path.GetFileName(file));

                        bool isProcessed = ProcessOutputXmlFile(file, connectionString);
                        string fileName = Path.GetFileName(file);

                        if (isProcessed)
                        {
                            File.Move(file, Path.Combine(archivePath, fileName), overwrite: true);
                            _logger.LogInformation("Çýktý XML'i ARCHIVE klasörüne taþýndý.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Çýktý (Output) klasörü taranýrken bir hata oluþtu.");
                }

                // 10 saniye bekleme süresi
                await Task.Delay(10000, stoppingToken);
            }
        }

        
        // YÖNTEM A: Yeni Gelen Sipariþ XML'lerini Kaydetme 
       
        private bool ProcessInputXmlFile(string filePath, string connectionString)
        {
            try
            {
                XDocument doc = XDocument.Load(filePath);

                string orderNo = doc.Root?.Element("OrderNo")?.Value ?? "";
                string orderDesc = doc.Root?.Element("OrderDesc")?.Value ?? "";
                string orderType = doc.Root?.Element("OrderType")?.Value ?? "Standard";
                string createdBy = doc.Root?.Element("CreatedBy")?.Value ?? "System";
                int quantity = int.Parse(doc.Root?.Element("OrderQuantity")?.Value ?? "1");

                if (string.IsNullOrEmpty(orderNo))
                {
                    _logger.LogWarning("Geçersiz Giriþ XML'i: OrderNo bulunamadý.");
                    return false;
                }

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    for (int i = 1; i <= quantity; i++)
                    {
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

                        _logger.LogInformation("Veritabanýna kaydedildi: {orderId}", splittedOrderId);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Giriþ XML'i iþlenirken hata oluþtu: {file}", filePath);
                return false;
            }
        }

        
        // YÖNTEM B: Tamamlanmýþ Sipariþ XML'ini Okuma ve Durumu Güncelleme
        
        private bool ProcessOutputXmlFile(string filePath, string connectionString)
        {
            try
            {
                XDocument doc = XDocument.Load(filePath);
                string orderId = doc.Root?.Element("OrderId")?.Value ?? "";

                if (string.IsNullOrEmpty(orderId))
                {
                    _logger.LogWarning("Geçersiz Çýktý XML'i: OrderId bulunamadý.");
                    return false;
                }

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "UPDATE Orders SET IsProcessed = 1 WHERE OrderId = @OrderId";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@OrderId", orderId);
                        int affectedRows = command.ExecuteNonQuery();

                        if (affectedRows > 0)
                        {
                            _logger.LogInformation("Veritabaný Güncellendi: Sipariþ {OrderId} için IsProcessed = True (1) yapýldý.", orderId);
                            return true;
                        }
                        else
                        {
                            _logger.LogWarning("Veritabanýnda güncellenecek '{OrderId}' kodlu bir sipariþ bulunamadý.", orderId);
                            return false;
                        }
                    }
                }
            }
            catch (IOException)
            {
                // Dosya o anda API tarafýndan hâlâ yazýlýyor olabilir. Bir sonraki turda tekrar denenecek.
                _logger.LogWarning("Dosya kilitli, sonraki turda tekrar denenecek: {file}", Path.GetFileName(filePath));
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Çýktý XML'i iþlenirken hata oluþtu: {file}", filePath);
                return false;
            }
        }
    }
}