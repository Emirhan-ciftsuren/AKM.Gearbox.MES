using System;
using System.Collections.Generic;
using System.Configuration; // <-- Referans eklediğimiz için artık bu çalışacak
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AKM.Gearbox.MES.Desktop.Models;

namespace AKM.Gearbox.MES.Desktop.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ApiService()
        {
            _httpClient = new HttpClient();

            // App.config dosyasındaki ApiBaseUrl key'ini okuyoruz.
            // Eğer bir aksilik olur da okunamazsa yedek plan olarak lokal portunu atıyoruz.
            _baseUrl = ConfigurationManager.AppSettings["ApiBaseUrl"] ?? "http://localhost:5293/api/orders";
        }

        // 1. GET - Tüm Siparişleri Listele
        public async Task<List<Order>> GetAllOrdersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(_baseUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    return JsonSerializer.Deserialize<List<Order>>(jsonString, options) ?? new List<Order>();
                }
                return new List<Order>();
            }
            catch (Exception ex)
            {
                throw new Exception($"API Bağlantı Hatası: {ex.Message}");
            }
        }

        // 2. POST - Yeni İş Emri Ekle
        public async Task<bool> CreateOrderAsync(Order order)
        {
            var json = JsonSerializer.Serialize(order);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_baseUrl, content);
            return response.IsSuccessStatusCode;
        }

        // 3. POST - Sipariş Güncelle / Status Değiştir
        public async Task<bool> UpdateOrderStatusAsync(string orderId, string status)
        {
            var payload = new { OrderId = orderId, Status = status };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/update-status", content);
            return response.IsSuccessStatusCode;
        }

        // 4. DELETE - Sipariş Sil
        public async Task<bool> DeleteOrderAsync(string orderId)
        {
            // API tarafında [HttpDelete("{id}")] olduğu için id parametresini direkt URL sonuna ekliyoruz
            var response = await _httpClient.DeleteAsync($"{_baseUrl}/{Uri.EscapeDataString(orderId)}");
            return response.IsSuccessStatusCode;
        }
    }
}