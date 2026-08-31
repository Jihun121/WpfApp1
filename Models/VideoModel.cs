using System;
using System.Collections.Generic;
using System.Text;

namespace WpfApp1.Models
{
    public class VideoModel
    {
        public int VideoId { get; set; }
        public string Title { get; set; }
        public string Genre { get; set; }

        // UI의 콤보박스나 리스트에서 장르와 제목을 한 번에 보여주기 위한 편의성 프로퍼티
        public string DisplayInfo => $"[{Genre}] {Title}";

        // 목록에 직관적으로 보여줄 상태 텍스트와 글자 색상
        public string StatusText { get; set; }
        public string StatusColor { get; set; }
    }
}
