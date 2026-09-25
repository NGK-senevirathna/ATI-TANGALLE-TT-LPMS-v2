// ============================================================
//  DepartmentModel.cs
//  FIX: DeptID changed from int → string
//       DB column is Short Text with pattern TAN/IT
//       DepartmentController.Save(), Edit(), Delete() all pass string DeptID
// ============================================================
using System.ComponentModel.DataAnnotations;

namespace TimetableApp.Models
{
    public class DepartmentModel
    {
        [Required(ErrorMessage = "Department ID is required")]
        [Display(Name = "Department ID")]
        public string DeptID { get; set; }          // was int — WRONG, DB is Short Text TAN/XX

        [Required(ErrorMessage = "Department name is required")]
        [Display(Name = "Department Name")]
        public string DeptName { get; set; }
    }
}
