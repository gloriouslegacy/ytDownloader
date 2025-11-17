using System.ComponentModel;

namespace ytDownloader.Models
{
    /// <summary>
    /// 예약된 채널 다운로드 데이터 모델
    /// </summary>
    public class ScheduledChannel : INotifyPropertyChanged
    {
        private bool _isSelected;

        /// <summary>채널 URL</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>채널 이름 (선택사항, 사용자가 입력)</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>추가된 날짜/시간</summary>
        public DateTime AddedDate { get; set; } = DateTime.Now;

        /// <summary>체크박스 선택 여부</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        /// <summary>
        /// 표시용 문자열
        /// </summary>
        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(Name))
            {
                return $"{Name} - {Url}";
            }
            return Url;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
