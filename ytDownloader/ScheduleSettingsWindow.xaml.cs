using System.Windows;
using System.Windows.Controls;
using ytDownloader.Services;

namespace ytDownloader
{
    public partial class ScheduleSettingsWindow : Window
    {
        private readonly TaskSchedulerService _schedulerService;

        public ScheduleSettingsWindow()
        {
            InitializeComponent();
            _schedulerService = new TaskSchedulerService();

            InitializeTimeComboBoxes();
            UpdateStatus();
        }

        /// <summary>
        /// 시간 콤보박스 초기화
        /// </summary>
        private void InitializeTimeComboBoxes()
        {
            // 시간 (0-23)
            for (int i = 0; i < 24; i++)
            {
                cmbHour.Items.Add(new ComboBoxItem { Content = $"{i:D2}시", Tag = i });
            }

            // 분 (0, 15, 30, 45)
            cmbMinute.Items.Add(new ComboBoxItem { Content = "00분", Tag = 0 });
            cmbMinute.Items.Add(new ComboBoxItem { Content = "15분", Tag = 15 });
            cmbMinute.Items.Add(new ComboBoxItem { Content = "30분", Tag = 30 });
            cmbMinute.Items.Add(new ComboBoxItem { Content = "45분", Tag = 45 });
        }

        /// <summary>
        /// 현재 스케줄 상태 업데이트
        /// </summary>
        private void UpdateStatus()
        {
            if (_schedulerService.IsTaskScheduled())
            {
                txtStatus.Text = "✅ 자동 예약이 등록되어 있습니다.\n작업 스케줄러에서 상세 정보를 확인할 수 있습니다.";
            }
            else
            {
                txtStatus.Text = "등록된 스케줄이 없습니다.";
            }
        }

        /// <summary>
        /// 등록 버튼 클릭
        /// </summary>
        private void btnRegister_Click(object sender, RoutedEventArgs e)
        {
            var selectedFrequency = cmbFrequency.SelectedItem as ComboBoxItem;
            var selectedHour = cmbHour.SelectedItem as ComboBoxItem;
            var selectedMinute = cmbMinute.SelectedItem as ComboBoxItem;

            if (selectedFrequency == null || selectedHour == null || selectedMinute == null)
            {
                MessageBox.Show("모든 항목을 선택해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int frequency = (int)selectedFrequency.Tag;
            int hour = (int)selectedHour.Tag;
            int minute = (int)selectedMinute.Tag;

            bool success = _schedulerService.CreateScheduledTask(frequency, hour, minute);

            if (success)
            {
                MessageBox.Show(
                    $"자동 예약이 등록되었습니다.\n\n" +
                    $"실행 주기: {selectedFrequency.Content}\n" +
                    $"실행 시간: {hour:D2}:{minute:D2}\n\n" +
                    $"예약된 채널들이 지정한 시간에 자동으로 다운로드됩니다.",
                    "등록 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                UpdateStatus();
            }
            else
            {
                MessageBox.Show(
                    "자동 예약 등록에 실패했습니다.\n관리자 권한이 필요할 수 있습니다.",
                    "등록 실패",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// 삭제 버튼 클릭
        /// </summary>
        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!_schedulerService.IsTaskScheduled())
            {
                MessageBox.Show("등록된 스케줄이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                "등록된 자동 예약을 삭제하시겠습니까?",
                "확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                bool success = _schedulerService.DeleteScheduledTask();

                if (success)
                {
                    MessageBox.Show("자동 예약이 삭제되었습니다.", "삭제 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateStatus();
                }
                else
                {
                    MessageBox.Show(
                        "자동 예약 삭제에 실패했습니다.\n관리자 권한이 필요할 수 있습니다.",
                        "삭제 실패",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
        }

        /// <summary>
        /// 닫기 버튼 클릭
        /// </summary>
        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
