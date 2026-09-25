// ============================================================
//  PreferenceModel.cs
//  FIX 1: Removed SubjectID (int) — preferences store SubjectCode (string)
//  FIX 2: Removed SeqOrder — DB has fixed columns 1stPref..5thPref, not a seq row per subject
//  FIX 3: LecturerID changed from int → string  (Short Text in DB)
//  FIX 4: Added Pref1Name..Pref5Name — used by GetAllPreferences() JOIN query in controller
//  PreferenceController.GetAllPreferences() maps:
//      PrefID, LecturerName, Pref1Name, Pref2Name, Pref3Name, Pref4Name, Pref5Name
// ============================================================

namespace TimetableApp.Models
{
    public class PreferenceModel
    {
        public int    PrefID       { get; set; }    // AutoNumber PK

        public string LecturerID   { get; set; }    // was int — WRONG, Short Text FK

        // Display-only — lecturer full name from JOIN
        public string LecturerName { get; set; }

        // The 5 preference columns store SubjectCode values (Short Text)
        // These display names are populated by the 5 LEFT JOINs in GetAllPreferences()
        public string Pref1Name    { get; set; }    // was SubjectName — too generic
        public string Pref2Name    { get; set; }
        public string Pref3Name    { get; set; }
        public string Pref4Name    { get; set; }
        public string Pref5Name    { get; set; }
    }
}
