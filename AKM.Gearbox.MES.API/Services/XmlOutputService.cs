using System;
using System.IO;
using System.Xml.Linq;

namespace AKM.Gearbox.MES.API.Services
{
    public class XmlOutputService
    {
        private readonly string _outputDirectory;

        public XmlOutputService()
        {
            // API'nin kendi altı yerine, bilgisayardaki ortak klasörün altındaki Output yolunu veriyoruz:
            _outputDirectory = @"C:\MES_Files\Output"; // <-- Gerçek ortak klasör yolun hangisiyse buraya yazabilirsin

            // Eğer klasör yoksa otomatik oluşturulur.
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
        }

        public void GenerateOrderCompletedXml(string orderId, string orderDesc, string createdBy)
        {
            try
            {
                // XML içeriğini dökümandaki use case'e göre hazırlıyoruz
                XDocument xmlDoc = new XDocument(
                    new XDeclaration("1.0", "utf-8", "yes"),
                    new XElement("OrderResult",
                        new XElement("OrderId", orderId),
                        new XElement("OrderDesc", orderDesc),
                        new XElement("OrderStatus", "Completed"),
                        new XElement("CompletionDate", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")),
                        new XElement("ProcessedBy", createdBy ?? "Operator")
                    )
                );

                // Dosya adını benzersiz yapmak için "Order_OrderId.xml" formatında kaydediyoruz.
                // Noktalı ID'ler (örn: 1000.1) dosya adında sorun yaratmaz.
                string fileName = $"Order_{orderId}.xml";
                string fullPath = Path.Combine(_outputDirectory, fileName);

                xmlDoc.Save(fullPath);
                Console.WriteLine($"[XML SUCCESS] ERP geri bildirim XML'i başarıyla oluşturuldu: {fullPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[XML ERROR] XML dosyası üretilirken hata oluştu: {ex.Message}");
                throw;
            }
        }
    }
}