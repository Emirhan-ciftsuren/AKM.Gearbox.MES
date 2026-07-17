using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;

namespace AKM.Gearbox.MES.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public OrdersController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // 1. GET METODU: Tüm Alanları SQL'den Eksiksiz Çeker
        [HttpGet]
        public IActionResult GetOrders()
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
            var orders = new List<object>();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    // Tablodaki tüm sütunları eksiksiz çekiyoruz
                    string query = "SELECT OrderId, OrderDesc, OrderCreationDate, OrderType, OrderStatus, CreatedBy, IsProcessed FROM Orders";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            orders.Add(new
                            {
                                OrderId = reader["OrderId"].ToString(),
                                OrderDesc = reader["OrderDesc"].ToString(),
                                OrderCreationDate = Convert.ToDateTime(reader["OrderCreationDate"]),
                                OrderType = reader["OrderType"].ToString(),
                                OrderStatus = reader["OrderStatus"].ToString(),
                                CreatedBy = reader["CreatedBy"].ToString(),
                                IsProcessed = Convert.ToBoolean(reader["IsProcessed"])
                            });
                        }
                    }
                }
                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Veritabanı listeleme hatası: {ex.Message}");
            }
        }

        // 2. POST METODU: Yeni İş Emri Ekleme (Eksik Olan Metot Eklendi)
        [HttpPost]
        public IActionResult CreateOrder([FromBody] CreateOrderRequest request)
        {
            if (string.IsNullOrEmpty(request.OrderId) || string.IsNullOrEmpty(request.OrderDesc))
            {
                return BadRequest("OrderId ve Açıklama alanları boş bırakılamaz.");
            }

            string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"INSERT INTO Orders (OrderId, OrderDesc, OrderCreationDate, OrderType, OrderStatus, CreatedBy, IsProcessed) 
                                    VALUES (@OrderId, @OrderDesc, @OrderCreationDate, @OrderType, @OrderStatus, @CreatedBy, @IsProcessed)";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@OrderId", request.OrderId);
                        cmd.Parameters.AddWithValue("@OrderDesc", request.OrderDesc);
                        cmd.Parameters.AddWithValue("@OrderCreationDate", request.OrderCreationDate);
                        cmd.Parameters.AddWithValue("@OrderType", request.OrderType ?? "Gearbox_Standard");
                        cmd.Parameters.AddWithValue("@OrderStatus", request.OrderStatus ?? "Pending");
                        cmd.Parameters.AddWithValue("@CreatedBy", request.CreatedBy ?? "Desktop_User");
                        cmd.Parameters.AddWithValue("@IsProcessed", request.IsProcessed);

                        cmd.ExecuteNonQuery();
                    }
                }
                return Ok(new { Message = $"İş emri {request.OrderId} başarıyla oluşturuldu." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Veritabanı ekleme hatası: {ex.Message}");
            }
        }

        // 3. POST METODU: Sipariş Durumu Güncelleme
        [HttpPost("update-status")]
        public IActionResult UpdateOrderStatus([FromBody] UpdateStatusRequest request)
        {
            if (string.IsNullOrEmpty(request.OrderId) || string.IsNullOrEmpty(request.Status))
            {
                return BadRequest("Sipariş Kodu ve Durum boş olamaz.");
            }

            string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";

            try
            {
                // XML oluştururken kullanacağımız veritabanı bilgilerini saklayacağımız değişkenler
                string orderDesc = "Açıklama Yok";
                string createdBy = "System";
                bool isSuccess = false;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // 1. ADIM: Sipariş Durumunu Güncelliyoruz
                    string updateQuery = "UPDATE Orders SET OrderStatus = @Status WHERE OrderId = @OrderId";

                    using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Status", request.Status);
                        cmd.Parameters.AddWithValue("@OrderId", request.OrderId);

                        int affectedRows = cmd.ExecuteNonQuery();
                        if (affectedRows == 0)
                        {
                            return NotFound($"Belirtilen OrderId ({request.OrderId}) bulunamadı.");
                        }

                        isSuccess = true; // Güncelleme başarıyla gerçekleşti
                    }

                    // 2. ADIM: Eğer durum "Completed" olarak güncellendiyse, XML'e yazmak üzere sipariş detaylarını SELECT ile çekiyoruz
                    if (isSuccess && request.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
                    {
                        string selectQuery = "SELECT OrderDesc, CreatedBY FROM Orders WHERE OrderId = @OrderId";

                        using (SqlCommand selectCmd = new SqlCommand(selectQuery, conn))
                        {
                            selectCmd.Parameters.AddWithValue("@OrderId", request.OrderId);

                            using (SqlDataReader reader = selectCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    // Veritabanındaki değerleri null kontrolü yaparak alıyoruz
                                    orderDesc = reader["OrderDesc"] != DBNull.Value ? reader["OrderDesc"].ToString() : "Açıklama Yok";
                                    createdBy = reader["CreatedBY"] != DBNull.Value ? reader["CreatedBY"].ToString() : "Operator";
                                }
                            }
                        }

                        // 3. ADIM: XML dosyasını üreten servisimizi çağırıyoruz
                        try
                        {
                            var xmlService = new Services.XmlOutputService();
                            xmlService.GenerateOrderCompletedXml(request.OrderId, orderDesc, createdBy);
                        }
                        catch (Exception xmlEx)
                        {
                            // XML üretilirken hata oluşursa logluyoruz ama API'nin istemciye başarılı dönmesini engellemiyoruz.
                            // Çünkü veritabanı kaydı başarıyla güncellendi.
                            Console.WriteLine($"[XML Tetikleme Hatası]: {xmlEx.Message}");
                        }
                    }
                }

                return Ok(new { Message = $"Sipariş {request.OrderId} başarıyla güncellendi ve gerekli süreçler tetiklendi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Veritabanı güncelleme hatası: {ex.Message}");
            }
        }

        // 4. DELETE METODU: Sipariş Silme (Eksik Olan Metot Eklendi)
        // Adres: DELETE http://localhost:5293/api/orders/1003.2
        [HttpDelete("{id}")]
        public IActionResult DeleteOrder(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest("OrderId boş olamaz.");
            }

            string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "DELETE FROM Orders WHERE OrderId = @OrderId";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@OrderId", id);

                        int affectedRows = cmd.ExecuteNonQuery();
                        if (affectedRows == 0)
                        {
                            return NotFound($"Silinmek istenen {id} kodlu sipariş bulunamadı.");
                        }
                    }
                }
                return Ok(new { Message = $"İş emri {id} başarıyla silindi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Veritabanı silme hatası: {ex.Message}");
            }
        }
    }

    // DTO Modelleri
    public class UpdateStatusRequest
    {
        public string OrderId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class CreateOrderRequest
    {
        public string OrderId { get; set; } = string.Empty;
        public string OrderDesc { get; set; } = string.Empty;
        public DateTime OrderCreationDate { get; set; }
        public string OrderType { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public bool IsProcessed { get; set; }
    }
}