using System;
using System.Collections.Generic;
using System.Text;

namespace WpfApp1.Models
{
    // Rentals, Customers, Videos 3개 테이블을 JOIN한 결과를 UI(DataGrid)에 매핑하기 위한 전용 클래스
    public class RentalModel
    {
        public int RentalId { get; set; }
        public string CustomerName { get; set; }
        public string VideoTitle { get; set; }
        public string RentalDate { get; set; }

        // 연체 여부를 담을 논리(bool) 속성
        public bool IsOverdue { get; set; }

        // 화면에 직관적으로 보여줄 상태 문자열 (예: "🚨 연체", "대여중")
        public string StatusText { get; set; }

        // 반납이 완료되었으면 날짜를, 아니면 "미반납" 등을 표시하기 위해 문자열(string)로 받습니다.
        public string ReturnDateDisplay { get; set; }
    }
}
