// ============================================================
//  SubjectModel.cs
//  FIX 1: Removed SubjectID (int) — DB uses SubjectCode as the PK (Short Text), no int ID
//  FIX 2: DeptID changed from int → string       (Short Text FK e.g. TAN/IT)
//  FIX 3: Added ContactHoursPerWeek (string)      (Short Text column per DB schema)
//  SubjectController uses: SubjectCode, SubjectName, DeptID(string), ContactHoursPerWeek, DeptName
// ============================================================
using System.ComponentModel.DataAnnotations;

namespace TimetableApp.Models
{
    public class SubjectModel
    {
        // SubjectCode is the Primary Key — Short Text, NOT an AutoNumber int
        [Required(ErrorMessage = "Subject code is required")]
        [Display(Name = "Subject Code")]
        public string SubjectCode { get; set; }     // was int SubjectID — WRONG

        [Required(ErrorMessage = "Subject name is required")]
        [Display(Name = "Subject Name")]
        public string SubjectName { get; set; }

        [Required(ErrorMessage = "Please select a department")]
        [Display(Name = "Department")]
        public string DeptID { get; set; }          // was int — WRONG, FK to DeptID Short Text

        [Display(Name = "Contact Hours / Week")]
        public string ContactHoursPerWeek { get; set; }  // was missing — Short Text in DB

        // Read-only display field — populated by JOIN in controller, not saved to DB
        [Display(Name = "Department")]
        public string DeptName { get; set; }
    }
}
