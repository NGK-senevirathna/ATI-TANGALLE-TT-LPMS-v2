// ============================================================
//  TimeSlotModel.cs  — matches YOUR actual DB schema exactly
//
//  YOUR TimeSlot table columns:
//    SlotID (AutoNumber), Duration (Text), SortOrder (Number)
//
//  NOTE: DayOfWeek is NOT in TimeSlot — it lives in the Timetable table.
//  DayOfWeek property kept here only for form use in TimeSlotController
//  but is NOT read from or written to the TimeSlot DB table.
// ============================================================
using System.ComponentModel.DataAnnotations;

namespace TimetableApp.Models
{
    public class TimeSlotModel
    {
        public int SlotID { get; set; }

        [Required(ErrorMessage = "Please select a duration")]
        [Display(Name = "Duration")]
        public string Duration { get; set; }

        public int SortOrder { get; set; }
    }
}