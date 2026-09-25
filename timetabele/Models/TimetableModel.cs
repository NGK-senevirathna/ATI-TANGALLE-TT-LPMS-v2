// ============================================================
//  TimetableModel.cs  — matches YOUR actual DB schema exactly
//
//  YOUR Timetable table columns:
//    TimetableID (AutoNumber), LecturerID (Text), SubjectCode (Text),
//    HallName (Text), SlotID (Number), Duration (Text), DayOfWeek (Text)
//
//  FIXES vs uploaded version:
//  1. HallID (int) removed — your DB stores HallName (string) directly
//  2. DayOfWeek kept as form binding field — form submits it separately
//  3. Duration kept as form binding — filled from SlotID lookup in controller
// ============================================================
using System.ComponentModel.DataAnnotations;

namespace TimetableApp.Models
{
    public class TimetableModel
    {
        public int TimetableID { get; set; }

        // ── Form binding fields (submitted by the Add form) ───────────────────
        [Required(ErrorMessage = "Please select a lecturer")]
        public string LecturerID { get; set; }

        [Required(ErrorMessage = "Please select a subject")]
        public string SubjectCode { get; set; }

        [Required(ErrorMessage = "Please select a hall")]
        public string HallName { get; set; }        // Text FK — NOT int HallID

        [Required(ErrorMessage = "Please select a time slot")]
        public int SlotID { get; set; }

        [Required(ErrorMessage = "Please select a day")]
        public string DayOfWeek { get; set; }       // submitted from day dropdown in form

        // ── Display-only (populated by JOINs in GetFullTimetable) ─────────────
        public string LecturerName { get; set; }
        public string SubjectName { get; set; }
        public string Duration { get; set; }    // from TimeSlot JOIN
        public int SortOrder { get; set; }    // from TimeSlot.SortOrder
    }
}