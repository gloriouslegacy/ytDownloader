using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using ytDownloader.Services;
using ytDownloader.Models;

namespace ytDownloader
{
    public partial class ScheduleSettingsWindow : Window
    {
        private readonly TaskSchedulerService _schedulerService;
        private readonly SettingsService _settingsService;
        private AppSettings _currentSettings;
        private ScheduleTaskInfo? _editingTask; // 편집 중인 스케줄 (null이면 새로 만들기 모드)

        public ScheduleSettingsWindow()
        {
            InitializeComponent();
            _schedulerService = new TaskSchedulerService();
            _settingsService = new SettingsService();
            _currentSettings = _settingsService.LoadSettings();

            InitializeFrequencyComboBox();
            InitializeTimeComboBoxes();
            InitializeSchedulerSettings();
            UpdateStatus();
        }

        /// <summary>
        /// 편집 모드로 스케줄 설정 창 열기
        /// </summary>
        public void LoadScheduleForEdit(ScheduleTaskInfo task)
        {
            _editingTask = task;

            // 스케줄러 설정 로드
            var settings = _settingsService.LoadSchedulerSettings(task.TaskName);
            if (settings != null)
            {
                txtSchedulerChannelUrl.Text = settings.ChannelUrl ?? "";
                txtSchedulerSavePath.Text = settings.SavePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                cmbSchedulerFormat.SelectedIndex = (int)settings.Format;
                chkSchedulerSubtitle.IsChecked = settings.DownloadSubtitle;
                SetComboBoxValue(cmbSchedulerSubtitleFormat, settings.SubtitleFormat ?? "srt");
                SetComboBoxValue(cmbSchedulerSubtitleLang, settings.SubtitleLang ?? "ko");
                chkSchedulerNotification.IsChecked = settings.EnableNotification;
                txtSchedulerMaxDownloads.Text = settings.MaxDownloads.ToString();
                chkSchedulerStructuredFolders.IsChecked = settings.UseStructuredFolder;
                chkSchedulerThumbnail.IsChecked = settings.SaveThumbnail;
                chkSchedulerSingleVideo.IsChecked = settings.SingleVideoOnly;
            }

            // 주기 및 시간 설정
            // FrequencyDays를 파싱하여 콤보박스 설정
            if (task.FrequencyDays.EndsWith("일마다"))
            {
                string daysStr = task.FrequencyDays.Replace("일마다", "").Trim();
                if (int.TryParse(daysStr, out int days))
                {
                    // cmbFrequency에서 해당 값 찾기
                    for (int i = 0; i < cmbFrequency.Items.Count; i++)
                    {
                        if (cmbFrequency.Items[i] is ComboBoxItem item && item.Tag is int tagValue && tagValue == days)
                        {
                            cmbFrequency.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }

            // 시간 설정 (ExecutionTime 형식: "HH:mm")
            if (!string.IsNullOrEmpty(task.ExecutionTime) && task.ExecutionTime.Contains(":"))
            {
                var timeParts = task.ExecutionTime.Split(':');
                if (timeParts.Length == 2 && int.TryParse(timeParts[0], out int hour) && int.TryParse(timeParts[1], out int minute))
                {
                    // 시간 콤보박스 설정
                    for (int i = 0; i < cmbHour.Items.Count; i++)
                    {
                        if (cmbHour.Items[i] is ComboBoxItem item && item.Tag is int tagValue && tagValue == hour)
                        {
                            cmbHour.SelectedIndex = i;
                            break;
                        }
                    }

                    // 분 콤보박스 설정
                    for (int i = 0; i < cmbMinute.Items.Count; i++)
                    {
                        if (cmbMinute.Items[i] is ComboBoxItem item && item.Tag is int tagValue && tagValue == minute)
                        {
                            cmbMinute.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }

            // 제목 변경
            this.Title = "자동 예약 편집";
        }

        /// <summary>
        /// 스케줄러 설정 초기화 (독립된 기본값으로)
        /// </summary>
        private void InitializeSchedulerSettings()
        {
            // 자동 예약 설정은 기본 설정값을 참조하지 않고 독립된 기본값 사용
            txtSchedulerChannelUrl.Text = "";
            txtSchedulerSavePath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            cmbSchedulerFormat.SelectedIndex = 0; // 최고화질
            chkSchedulerSubtitle.IsChecked = true;
            SetComboBoxValue(cmbSchedulerSubtitleFormat, "srt");
            SetComboBoxValue(cmbSchedulerSubtitleLang, "ko");
            chkSchedulerNotification.IsChecked = true;
            txtSchedulerMaxDownloads.Text = "5";
            chkSchedulerStructuredFolders.IsChecked = true;
            chkSchedulerThumbnail.IsChecked = true;
            chkSchedulerSingleVideo.IsChecked = false;
        }

        /// <summary>
        /// ComboBox에 값 설정
        /// </summary>
        private void SetComboBoxValue(ComboBox comboBox, string value)
        {
            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                var item = comboBox.Items[i];
                string itemValue = "";

                if (item is ComboBoxItem comboBoxItem)
                {
                    itemValue = comboBoxItem.Content?.ToString() ?? "";
                }
                else
                {
                    itemValue = item?.ToString() ?? "";
                }

                if (itemValue == value)
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
            }
            comboBox.Text = value;
        }

        /// <summary>
        /// ComboBox에서 값 가져오기
        /// </summary>
        private string GetComboBoxValue(ComboBox comboBox)
        {
            if (comboBox.SelectedItem != null)
            {
                if (comboBox.SelectedItem is ComboBoxItem item)
                {
                    return item.Content?.ToString() ?? "";
                }
                return comboBox.SelectedItem.ToString() ?? "";
            }
            return comboBox.Text ?? "";
        }

        /// <summary>
        /// URL 붙여넣기 버튼
        /// </summary>
        private void btnPasteChannelUrl_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.Clipboard.ContainsText())
            {
                txtSchedulerChannelUrl.Text = System.Windows.Clipboard.GetText();
            }
        }

        /// <summary>
        /// 저장 경로 찾기 버튼
        /// </summary>
        [SupportedOSPlatform("windows")]
        private void btnBrowseSavePath_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    txtSchedulerSavePath.Text = dialog.SelectedPath;
                }
            }
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
                cmbHour.Items.Add(new ComboBoxItem { Content = $"{i:D2}", Tag = i });
            }

            // 분 (0-59)
            for (int i = 0; i < 60; i++)
            {
                cmbMinute.Items.Add(new ComboBoxItem { Content = $"{i:D2}분", Tag = i });
            }
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
                    // 객체를 직접 추가 (ToString()이 자동으로 호출됨)
                    lstScheduledTasks.Items.Add(task);
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

            // 입력값 검증
            if (string.IsNullOrWhiteSpace(txtSchedulerChannelUrl.Text))
            {
                MessageBox.Show("채널 URL을 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtSchedulerSavePath.Text))
            {
                MessageBox.Show("저장 경로를 선택해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtSchedulerMaxDownloads.Text, out int maxDownloads) || maxDownloads < 0)
            {
                MessageBox.Show("최대 다운로드 개수를 올바르게 입력해주세요. (0은 무한)", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int frequency = (int)selectedFrequency.Tag;
            int hour = (int)selectedHour.Tag;
            int minute = (int)selectedMinute.Tag;

            // 편집 모드일 때 기존 스케줄 삭제
            if (_editingTask != null)
            {
                bool deleteSuccess = _schedulerService.DeleteScheduledTask(_editingTask.TaskName);
                if (deleteSuccess)
                {
                    _settingsService.DeleteSchedulerSettings(_editingTask.TaskName);
                }
            }

            string? taskName = _schedulerService.CreateScheduledTask(frequency, hour, minute);

            if (taskName != null)
            {
                // 스케줄러별 설정 저장 (개별 파일로)
                var schedulerSettings = new SchedulerSettings
                {
                    TaskName = taskName,
                    ChannelUrl = txtSchedulerChannelUrl.Text.Trim(),
                    SavePath = txtSchedulerSavePath.Text,
                    Format = (VideoFormat)(cmbSchedulerFormat.SelectedIndex >= 0 ? cmbSchedulerFormat.SelectedIndex : 0),
                    DownloadSubtitle = chkSchedulerSubtitle.IsChecked ?? false,
                    SubtitleFormat = GetComboBoxValue(cmbSchedulerSubtitleFormat),
                    SubtitleLang = GetComboBoxValue(cmbSchedulerSubtitleLang),
                    EnableNotification = chkSchedulerNotification.IsChecked ?? true,
                    MaxDownloads = maxDownloads,
                    UseStructuredFolder = chkSchedulerStructuredFolders.IsChecked ?? true,
                    SaveThumbnail = chkSchedulerThumbnail.IsChecked ?? false,
                    SingleVideoOnly = chkSchedulerSingleVideo.IsChecked ?? false
                };

                // 개별 파일로 저장
                _settingsService.SaveSchedulerSettings(schedulerSettings);

                string successMessage = _editingTask != null
                    ? $"자동 예약이 수정되었습니다.\n\n" +
                      $"실행 주기: {selectedFrequency.Content}\n" +
                      $"실행 시간: {hour:D2}:{minute:D2}\n\n" +
                      $"예약된 채널들이 지정한 시간에 자동으로 다운로드됩니다."
                    : $"자동 예약이 등록되었습니다.\n\n" +
                      $"실행 주기: {selectedFrequency.Content}\n" +
                      $"실행 시간: {hour:D2}:{minute:D2}\n\n" +
                      $"예약된 채널들이 지정한 시간에 자동으로 다운로드됩니다.";

                string successTitle = _editingTask != null ? "수정 완료" : "등록 완료";

                MessageBox.Show(successMessage, successTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                UpdateStatus();
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                string errorMessage = _editingTask != null
                    ? "자동 예약 수정에 실패했습니다.\n관리자 권한이 필요할 수 있습니다."
                    : "자동 예약 등록에 실패했습니다.\n관리자 권한이 필요할 수 있습니다.";

                string errorTitle = _editingTask != null ? "수정 실패" : "등록 실패";

                MessageBox.Show(errorMessage, errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 선택 삭제 버튼 클릭
        /// </summary>
        private void btnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (lstScheduledTasks.SelectedItem == null)
            {
                MessageBox.Show("삭제할 스케줄을 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // "등록된 스케줄이 없습니다." 문자열 체크
            if (lstScheduledTasks.SelectedItem is string)
            {
                MessageBox.Show("삭제할 스케줄을 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // ScheduleTaskInfo 객체로 캐스팅
            if (lstScheduledTasks.SelectedItem is not ScheduleTaskInfo selectedTask)
            {
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
                bool success = _schedulerService.DeleteScheduledTask(selectedTask.TaskName);

                if (success)
                {
                    // 관련 스케줄러 설정 파일도 함께 삭제
                    _settingsService.DeleteSchedulerSettings(selectedTask.TaskName);

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
        /// 전체 삭제 버튼 클릭
        /// </summary>
        private void btnDeleteAll_Click(object sender, RoutedEventArgs e)
        {
            var tasks = _schedulerService.GetAllScheduledTasks();

            if (tasks.Count == 0)
            {
                MessageBox.Show("삭제할 스케줄이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"모든 자동 예약({tasks.Count}개)을 삭제하시겠습니까?",
                "전체 삭제 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                // 각 태스크의 설정 파일도 함께 삭제
                foreach (var task in tasks)
                {
                    _settingsService.DeleteSchedulerSettings(task.TaskName);
                }

                int deletedCount = _schedulerService.DeleteAllScheduledTasks();

                if (deletedCount > 0)
                {

                    MessageBox.Show(
                        $"{deletedCount}개의 자동 예약이 삭제되었습니다.",
                        "삭제 완료",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
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
