using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace AKM.Gearbox.MES.API.Controllers
{
    [ApiController] // Bu sınıfın bir API kapısı olduğunu .NET'e bildiriyoruz.
    [Route("api/[controller]")] // Tarayıcıdan erişim adresi: api/orders
    public class OrdersController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        // API projesi ayağa kalkarken appsettings.json'ı okuyabilmek için burayı kullanacağız
        public OrdersController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // 1. GET METODU: Tarayıcıdan bu adrese gelindiğinde çalışacak kod
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
                    // SQL'den sipariş bilgilerini çekiyoruz
                    string query = "SELECT OrderId, OrderDesc, OrderStatus FROM Orders";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Her bir siparişi listemize ekliyoruz
                            orders.Add(new
                            {
                                OrderId = reader["OrderId"].ToString(),
                                OrderDesc = reader["OrderDesc"].ToString(),
                                OrderStatus = reader["OrderStatus"].ToString()
                            });
                        }
                    }
                }

                // Her şey yolundaysa, sipariş listesini JSON formatında 200 (Başarılı) koduyla geri gönder
                return Ok(orders);
            }
            catch (Exception ex)
            {
                // Bir hata olursa, çökmek yerine hatayı dış dünyaya bildir
                return StatusCode(500, $"Veritabanı hatası: {ex.Message}");
            }
        }
        // 2. POST METODU: Dışarıdan gelen sipariş güncelleme isteklerini karşılar
        // Adres: POST http://localhost:5293/api/orders/update-status
        [HttpPost("update-status")]
        public IActionResult UpdateOrderStatus([FromBody] UpdateStatusRequest request)
        {
            // Gelen istekte sipariş kodu veya durum boşsa hata dön
            if (string.IsNullOrEmpty(request.OrderId) || string.IsNullOrEmpty(request.Status))
            {
                return BadRequest("Sipariş Kodu (OrderId) ve Durum (Status) boş olamaz.");
            }

            string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // SQL Update sorgusu: İlgili siparişin durumunu güncelliyoruz
                    string query = "UPDATE Orders SET OrderStatus = @Status WHERE OrderId = @OrderId";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        // SQL Injection güvenliği için parametre kullanıyoruz
                        cmd.Parameters.AddWithValue("@Status", request.Status);
                        cmd.Parameters.AddWithValue("@OrderId", request.OrderId);

                        int affectedRows = cmd.ExecuteNonQuery();

                        // Eğer veritabanında böyle bir sipariş kodu bulunamadıysa
                        if (affectedRows == 0)
                        {
                            return NotFound($"Belirtilen OrderId ({request.OrderId}) bulunamadı.");
                        }
                    }
                }

                // Güncelleme başarılıysa 200 Başarılı kodu dön ve mesaj ver
                return Ok(new { Message = $"Sipariş {request.OrderId} başarıyla '{request.Status}' olarak güncellendi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Veritabanı güncelleme hatası: {ex.Message}");
            }
        }
    } // OrdersController sınıfının sonu

    // API'ye dışarıdan gönderilecek veri formatını (şablonunu) tanımlıyoruz
    public class UpdateStatusRequest
    {
        public string OrderId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
    
