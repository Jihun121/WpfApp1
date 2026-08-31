using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace WpfApp1.Models
{
    // DB의 Customers 테이블 구조를 C#에서 1:1로 다루기 위한 뼈대(Model) 역할
    public class CustomerModel
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string PhoneNumber { get; set; } // DB에서 가져온 원본 숫자 데이터

        // 원본 숫자 데이터 무결성을 유지하면서, 화면(DataGrid)에만 전화번호 양식으로 표시용(Display) 속성 추가
        public string DisplayPhoneNumber
        {
            get
            {
                if (string.IsNullOrEmpty(PhoneNumber)) return PhoneNumber;

                // 11자리 (예: 01012345678 -> 010-1234-5678)
                if (PhoneNumber.Length == 11)
                    return Regex.Replace(PhoneNumber, @"(\d{3})(\d{4})(\d{4})", "$1-$2-$3");

                // 10자리 (예: 0212345678 -> 02-1234-5678)
                if (PhoneNumber.Length == 10)
                    return Regex.Replace(PhoneNumber, @"(\d{2,3})(\d{3,4})(\d{4})", "$1-$2-$3");

                return PhoneNumber; // 자릿수가 맞지 않으면 원본 그대로 출력
            }
        }
        public string Email { get; set; }
        public string CreatedDate { get; set; } // 화면에 깔끔하게 보여주기 위해 string으로 변환해서 담을 예정

        // 실시간 등급과 대여 횟수를 담을 속성
        public string CustomerGrade { get; set; }
        public int TotalRentals { get; set; }
    }
}
