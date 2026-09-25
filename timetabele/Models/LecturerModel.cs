// ============================================================
//  LecturerModel.cs
//  FIX 1: LecturerID changed from int → string  (Short Text PK in DB)
//  FIX 2: DeptID changed from int → string      (Short Text FK e.g. TAN/IT)
//  FIX 3: Added DeptID property for form binding (was missing)
//  LecturerController uses: LecturerID(string), FullName, Email, DeptID(string), DeptName
// ============================================================
using System.ComponentModel.DataAnnotations;

namespace TimetableApp.Models
{
    public class LecturerModel
    {
        [Required(ErrorMessage = "Lecturer ID is required")]
        [Display(Name = "Lecturer ID")]
        public string LecturerID { get; set; }      // was int — WRONG, DB is Short Text

        [Required(ErrorMessage = "Full name is required")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [EmailAddress(ErrorMessage = "Enter a valid email address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Please select a department")]
        [Display(Name = "Department")]
        public string DeptID { get; set; }          // was int — WRONG, FK to DeptID Short Text

        // Read-only display field — populated by JOIN in controller, not saved to DB
        [Display(Name = "Department")]
        public string DeptName { get; set; }
    }
}
