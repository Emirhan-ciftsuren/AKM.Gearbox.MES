using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AKM.Gearbox.MES.Desktop.Models;
using AKM.Gearbox.MES.Desktop.Services;

namespace AKM.Gearbox.MES.Desktop
{
    public partial class MainWindow : Window
    {
        private readonly ApiService _apiService;

        public MainWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();

            // Uygulama açıldığında verileri otomatik yükle
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadOrdersAsync();
        }

        // 1. API'den Siparişleri Çekip Grid'e Bağlayan Metot
        private async System.Threading.Tasks.Task LoadOrdersAsync()
        {
            try
            {
                var orders = await _apiService.GetAllOrdersAsync();
                DgOrders.ItemsSource = orders;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Veriler yüklenirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 2. Yenile (Refresh) Butonu
        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadOrdersAsync(); // Listeyi yeniden yükle
            ClearForm();             // Seçimi sıfırla, form alanlarını temizle ve ID kilidini aç!
        }

        // 3. Tablodan Seçilen Satırı Form Alanlarına Doldurma
        private void DgOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgOrders.SelectedItem is Order selectedOrder)
            {
                TxtOrderId.Text = selectedOrder.OrderId;
                TxtOrderId.IsEnabled = false; // Güncelleme yaparken ID değiştirilemesin diye kilitliyoruz
                TxtDescription.Text = selectedOrder.OrderDesc;

                // ComboBox değerlerini eşitleme
                SetComboBoxValue(CmbOrderType, selectedOrder.OrderType);
                SetComboBoxValue(CmbStatus, selectedOrder.OrderStatus);
            }
        }

        // ComboBox seçimi için yardımcı fonksiyon
        private void SetComboBoxValue(ComboBox comboBox, string value)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Content.ToString() == value)
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }
        }

        // Formu temizleme yardımcı metodu
        private void ClearForm()
        {
            TxtOrderId.Text = string.Empty;
            TxtOrderId.IsEnabled = true; // Yeni kayıt için ID alanını tekrar açıyoruz
            TxtDescription.Text = string.Empty;
            CmbOrderType.SelectedIndex = 0;
            CmbStatus.SelectedIndex = 0;
            DgOrders.SelectedItem = null;
        }

        // 4. Kaydet (Save) Butonu (Hem Ekleme Hem Güncelleme)
        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtOrderId.Text) || string.IsNullOrWhiteSpace(TxtDescription.Text))
            {
                MessageBox.Show("Lütfen İş Emri ID ve Açıklama alanlarını doldurun!", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string orderId = TxtOrderId.Text.Trim();
            string description = TxtDescription.Text.Trim();
            string orderType = (CmbOrderType.SelectedItem as ComboBoxItem)?.Content.ToString();
            string status = (CmbStatus.SelectedItem as ComboBoxItem)?.Content.ToString();

            // Eğer TxtOrderId kilitliyse (IsEnabled = false), bu bir güncelleme işlemidir.
            if (!TxtOrderId.IsEnabled)
            {
                // Mevcut siparişin durumunu güncelliyoruz
                bool success = await _apiService.UpdateOrderStatusAsync(orderId, status);
                if (success)
                {
                    MessageBox.Show("İş emri durumu başarıyla güncellendi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadOrdersAsync();
                    ClearForm();
                }
                else
                {
                    MessageBox.Show("Güncelleme işlemi başarısız oldu.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                // Yeni bir iş emri oluşturuyoruz
                var newOrder = new Order
                {
                    OrderId = orderId,
                    OrderDesc = description,
                    OrderCreationDate = DateTime.Now,
                    OrderType = orderType,
                    OrderStatus = status,
                    CreatedBy = "Admin_Desktop", // Masaüstü panelinden eklendiğini belirtmek için
                    IsProcessed = false
                };

                bool success = await _apiService.CreateOrderAsync(newOrder);
                if (success)
                {
                    MessageBox.Show("Yeni iş emri başarıyla oluşturuldu.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadOrdersAsync();
                    ClearForm();
                }
                else
                {
                    MessageBox.Show("Yeni iş emri oluşturulurken bir hata oluştu.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // 5. Sil (Delete) Butonu
        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (DgOrders.SelectedItem is Order selectedOrder)
            {
                var result = MessageBox.Show($"{selectedOrder.OrderId} ID'li iş emrini silmek istediğinize emin misiniz?",
                    "Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    bool success = await _apiService.DeleteOrderAsync(selectedOrder.OrderId);
                    if (success)
                    {
                        MessageBox.Show("İş emri başarıyla silindi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadOrdersAsync();
                        ClearForm();
                    }
                    else
                    {
                        MessageBox.Show("Silme işlemi başarısız oldu.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Lütfen silmek istediğiniz iş emrini tablodan seçin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}