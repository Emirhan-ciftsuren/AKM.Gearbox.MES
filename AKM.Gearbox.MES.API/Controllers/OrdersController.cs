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

    }
}