// ============================================================
//  LectureHallModel.cs
//  HallID  : int    (AutoNumber PK — correct, no change needed)
//  HallName: string (Short Text — correct)
//  Capacity: int    (Number — correct)
//  LectureHallController uses all three properties as-is
// ============================================================
using System.ComponentModel.DataAnnotations;

namespace TimetableApp.Models
{
    public class LectureHallModel
    {
        public int HallID { get; set; }                 // AutoNumber PK — int is correct

        [Required(ErrorMessage = "Hall name is required")]
        [Display(Name = "Hall Name")]
        public string HallName { get; set; }

        [Required(ErrorMessage = "Capacity is required")]
        [Range(1, 9999, ErrorMessage = "Capacity must be greater than 0")]
        public int Capacity { get; set; }
    }
}
