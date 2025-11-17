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

            InitializeFrequencyComboBox();
            InitializeTimeComboBoxes();
            UpdateStatus();
        }

        /// <summary>
        /// 실행 주기 콤보박스 초기화
        /// </summary>
        private void InitializeFrequencyComboBox()
        {
            // 콤보박스 항목 추가 (Tag를 int로 설정)
            cmbFrequency.Items.Add(new ComboBoxItem { Content = "매일 (1일)", Tag = 1 });
            cmbFrequency.Items.Add(new ComboBoxItem { Content = "2일마다", Tag = 2 });
            cmbFrequency.Items.Add(new ComboBoxItem { Content = "3일마다", Tag = 3 });
            cmbFrequency.Items.Add(new ComboBoxItem { Content = "4일마다", Tag = 4 });
            cmbFrequency.Items.Add(new ComboBoxItem { Content = "5일마다", Tag = 5 });
            cmbFrequency.Items.Add(new ComboBoxItem { Content = "6일마다", Tag = 6 });
            cmbFrequency.Items.Add(new ComboBoxItem { Content = "매주 (7일)", Tag = 7 });
            cmbFrequency.Items.Add(new ComboBoxItem { Content = "10일마다", Tag = 10 });
            cmbFrequency.Items.Add(new ComboBoxItem { Content = "2주마다 (14일)", Tag = 14 });
            cmbFrequency.Items.Add(new ComboBoxItem { Content = "3주마다 (21일)", Tag = 21 });
            cmbFrequency.Items.Add(new ComboBoxItem { Content = "매월 (30일)", Tag = 30 });
            cmbFrequency.Items.Add(new ComboBoxItem { Content = "31일마다", Tag = 31 });

            cmbFrequency.SelectedIndex = 0;
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
        /// 스케줄 목록 새로고침
        /// </summary>
        private void UpdateStatus()
        {
            lstScheduledTasks.Items.Clear();

            var tasks = _schedulerService.GetAllScheduledTasks();
            if (tasks.Count == 0)
            {
                lstScheduledTasks.Items.Add("등록된 스케줄이 없습니다.");
            }
            else
            {
                foreach (var task in tasks)
                {
                    lstScheduledTasks.Items.Add(task.DisplayText);
                }
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

            string? taskName = _schedulerService.CreateScheduledTask(frequency, hour, minute);

            if (taskName != null)
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
        /// 선택 삭제 버튼 클릭
        /// </summary>
        private void btnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (lstScheduledTasks.SelectedItem == null || lstScheduledTasks.SelectedItem.ToString() == "등록된 스케줄이 없습니다.")
            {
                MessageBox.Show("삭제할 스케줄을 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                "선택한 자동 예약을 삭제하시겠습니까?",
                "확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                var tasks = _schedulerService.GetAllScheduledTasks();
                int selectedIndex = lstScheduledTasks.SelectedIndex;

                if (selectedIndex >= 0 && selectedIndex < tasks.Count)
                {
                    var taskToDelete = tasks[selectedIndex];
                    bool success = _schedulerService.DeleteScheduledTask(taskToDelete.TaskName);

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
        }

        /// <summary>
        /// 새로고침 버튼 클릭
        /// </summary>
        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatus();
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
