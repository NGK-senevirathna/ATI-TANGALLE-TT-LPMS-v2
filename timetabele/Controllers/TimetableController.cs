using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using TimetableApp.Models;

namespace TimetableApp.Controllers
{
    public class TimetableController : Controller
    {
        // ---- Constants describing the weekly schedule shape ----
        private const int SlotsPerDay = 10;
        private const string LunchDuration = "12.30-1.00";

        private static readonly string[] WeekDays =
        {
            "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
        };

        private readonly string _connectionString;

        public TimetableController(IConfiguration configuration)
        {
            string databaseFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "timetable.accdb");
            _connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databaseFile};";
        }

        // GET: /Timetable
        public IActionResult Index()
        {
            ViewBag.Lecturers = LoadDropdownOptions("SELECT LecturerID, FullName FROM Lecturer ORDER BY FullName", "LecturerID", "FullName");
            ViewBag.Subjects = LoadDropdownOptions("SELECT SubjectCode, SubjectName FROM Subject ORDER BY SubjectName", "SubjectCode", "SubjectName");
            ViewBag.Halls = LoadDropdownOptions("SELECT HallName, HallName FROM LectureHall ORDER BY HallName", "HallName", "HallName");
            ViewBag.Slots = BuildSelectableTimeSlots();
            ViewBag.Days = WeekDays.ToList();
            ViewBag.Timetable = GetFullTimetable();

            // Per-lecturer ordered preference list (1st..5th) and which lecturer
            // (if any) already "owns" each subject. The Subject dropdown is
            // populated client-side from these once a Lecturer is chosen:
            //   - lecturer HAS preferences  -> show only those, ranked 1st..5th
            //   - lecturer has NO preferences -> fall back to the full subject list
            //   - either way, a subject already taken by a DIFFERENT lecturer is hidden
            ViewBag.LecturerPreferencesJson = System.Text.Json.JsonSerializer.Serialize(BuildLecturerPreferenceMap());
            ViewBag.AssignedSubjectsJson = System.Text.Json.JsonSerializer.Serialize(BuildAssignedSubjectsMap());

            return View();
        }

        // POST: /Timetable/Save
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Save(TimetableModel model, int SlotPosition, int PeriodCount)
        {
            if (PeriodCount < 1) PeriodCount = 1;

            string validationError = ValidateNewEntry(model, SlotPosition, PeriodCount, out List<int> globalSlotIds);
            if (validationError != null)
            {
                TempData["Error"] = validationError;
                return RedirectToAction("Index");
            }

            try
            {
                using var connection = new OleDbConnection(_connectionString);
                connection.Open();

                // Enforce the same rules server-side (never trust the client
                // dropdown alone — it can be bypassed). A lecturer who has not
                // set ANY preferences yet is allowed to be assigned any subject
                // (mirrors the dropdown fallback), so the preference-membership
                // check only applies once they HAVE at least one saved preference.
                if (HasAnyPreferences(connection, model.LecturerID) &&
                    !IsSubjectInLecturerPreferences(connection, model.LecturerID, model.SubjectCode))
                {
                    TempData["Error"] = "This subject is not in the selected lecturer's preference list.";
                    return RedirectToAction("Index");
                }

                string existingOwner = GetSubjectOwner(connection, model.SubjectCode);
                if (existingOwner != null && existingOwner != model.LecturerID)
                {
                    TempData["Error"] = "This subject has already been assigned to another lecturer.";
                    return RedirectToAction("Index");
                }

                // Pre-check EVERY period in the requested range before inserting
                // any of them, so a multi-period request either succeeds
                // completely or not at all — never a half-added block. A
                // (Day, SlotID) can only ever hold ONE class, period — no two
                // lecturers/subjects can be scheduled into the same slot.
                foreach (int slotId in globalSlotIds)
                {
                    if (HasClash(connection, model.LecturerID, slotId, model.DayOfWeek))
                    {
                        TempData["Error"] = "Lecturer clash detected in the selected period range.";
                        return RedirectToAction("Index");
                    }

                    string slotOwner = GetSlotOwner(connection, slotId, model.DayOfWeek);
                    if (slotOwner != null && slotOwner != model.LecturerID)
                    {
                        TempData["Error"] = "One of the selected time periods is already taken by another lecturer.";
                        return RedirectToAction("Index");
                    }
                }

                foreach (int slotId in globalSlotIds)
                {
                    model.SlotID = slotId;
                    InsertTimetableEntry(connection, model);
                }

                TempData["Message"] = globalSlotIds.Count > 1
                    ? $"Timetable entry added across {globalSlotIds.Count} consecutive periods!"
                    : "Timetable entry added successfully!";
            }
            catch (OleDbException ex)
            {
                TempData["Error"] = "Could not save the entry: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // GET: /Timetable/MyTimetable?lecturerId=...
        // A single lecturer's own personal, printable weekly schedule.
        public IActionResult MyTimetable(string lecturerId)
        {
            if (string.IsNullOrWhiteSpace(lecturerId))
            {
                TempData["Error"] = "Please select a lecturer first.";
                return RedirectToAction("Index");
            }

            List<TimetableModel> myEntries = GetFullTimetable()
                .Where(e => e.LecturerID == lecturerId)
                .ToList();

            ViewBag.TimeSlots = SlotData.GetAllSlots().OrderBy(s => s.SortOrder).Take(SlotsPerDay).ToList();
            ViewBag.LecturerName = myEntries.FirstOrDefault()?.LecturerName ?? GetLecturerName(lecturerId);

            return View(myEntries);
        }

        // GET: /Timetable/Weekly — grid view (Slot rows x Day columns)
        public IActionResult Weekly()
        {
            List<TimetableModel> timetable = GetFullTimetable();

            // Only the 10 canonical time-of-day rows are needed. SlotID is a
            // GLOBAL number (1-70, unique per day+time), so the grid must be
            // keyed by the Duration TEXT (e.g. "8.30-9.30") instead — otherwise
            // 70 rows would be needed rather than 10.
            ViewBag.TimeSlots = SlotData.GetAllSlots().OrderBy(s => s.SortOrder).Take(SlotsPerDay).ToList();
            ViewBag.TimetableGrid = BuildWeeklyGrid(timetable);

            return View(timetable);
        }

        // GET: /Timetable/Print — formal printable timetable
        public IActionResult Print()
        {
            List<TimetableModel> timetable = GetFullTimetable();
            ViewBag.TimeSlots = SlotData.GetAllSlots().OrderBy(s => s.SortOrder).Take(SlotsPerDay).ToList();

            return View(timetable);
        }

        // POST: /Timetable/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int timetableID)
        {
            if (timetableID <= 0)
            {
                TempData["Error"] = "Invalid entry.";
                return RedirectToAction("Index");
            }

            try
            {
                using var connection = new OleDbConnection(_connectionString);
                connection.Open();

                using var deleteCommand = new OleDbCommand("DELETE FROM Timetable WHERE TimetableID=?", connection);
                deleteCommand.Parameters.AddWithValue("?", timetableID);
                deleteCommand.ExecuteNonQuery();

                TempData["Message"] = "Timetable entry removed.";
            }
            catch (OleDbException ex)
            {
                TempData["Error"] = "Could not delete the entry: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // ===================================================================
        //  Private helpers
        // ===================================================================

        /// <summary>
        /// Validates the incoming form data for a new timetable entry and
        /// computes the resulting global SlotIDs (1-70) for every period in
        /// the requested range. Returns null when valid, or a user-facing
        /// error message when not.
        /// </summary>
        private string ValidateNewEntry(TimetableModel model, int slotPosition, int periodCount, out List<int> globalSlotIds)
        {
            globalSlotIds = new List<int>();

            if (model == null)
                return "Invalid submission.";

            if (string.IsNullOrWhiteSpace(model.LecturerID) ||
                string.IsNullOrWhiteSpace(model.SubjectCode) ||
                string.IsNullOrWhiteSpace(model.HallName) ||
                string.IsNullOrWhiteSpace(model.DayOfWeek))
            {
                return "Please select all fields.";
            }

            int dayIndex = Array.IndexOf(WeekDays, model.DayOfWeek);
            if (dayIndex < 0)
                return "Invalid day selected.";

            if (slotPosition < 1 || slotPosition > SlotsPerDay)
                return "Invalid time slot selected.";

            if (periodCount < 1 || periodCount > SlotsPerDay)
                return "Invalid number of periods.";

            int endPosition = slotPosition + periodCount - 1;
            if (endPosition > SlotsPerDay)
            {
                int available = SlotsPerDay - slotPosition + 1;
                return $"Not enough periods left in the day — only {available} period(s) available from this start time.";
            }

            // Lunch sits at position 5 ("12.30-1.00") — a class range may
            // never cross it (a class can't run through the lunch break).
            const int LunchPosition = 5;
            for (int pos = slotPosition; pos <= endPosition; pos++)
            {
                if (pos == LunchPosition)
                    return "The selected period range crosses the lunch break — please choose a range that doesn't include it.";

                globalSlotIds.Add((dayIndex * SlotsPerDay) + pos);
            }

            return null;
        }

        private bool HasClash(OleDbConnection connection, string lecturerId, int slotId, string dayOfWeek)
        {
            using var clashCommand = new OleDbCommand(
                "SELECT COUNT(*) FROM Timetable WHERE LecturerID=? AND SlotID=? AND DayOfWeek=?", connection);
            clashCommand.Parameters.AddWithValue("?", lecturerId);
            clashCommand.Parameters.AddWithValue("?", slotId);
            clashCommand.Parameters.AddWithValue("?", dayOfWeek);

            return (int)clashCommand.ExecuteScalar() > 0;
        }

        /// <summary>
        /// True if this lecturer has saved at least one preference row at all
        /// (regardless of which subjects). Used to decide whether the
        /// preference-membership check applies, or the "no preferences yet"
        /// fallback (any subject allowed) applies instead.
        /// </summary>
        private bool HasAnyPreferences(OleDbConnection connection, string lecturerId)
        {
            using var cmd = new OleDbCommand(
                "SELECT COUNT(*) FROM LecturerPreference WHERE LecturerID=?", connection);
            cmd.Parameters.AddWithValue("?", lecturerId);
            return (int)cmd.ExecuteScalar() > 0;
        }

        /// <summary>
        /// True if subjectCode appears anywhere in the given lecturer's
        /// 1st-5th preference columns.
        /// </summary>
        private bool IsSubjectInLecturerPreferences(OleDbConnection connection, string lecturerId, string subjectCode)
        {
            using var cmd = new OleDbCommand(
                @"SELECT COUNT(*) FROM LecturerPreference
                  WHERE LecturerID=? AND
                        ([1stPref]=? OR [2ndPref]=? OR [3rdPref]=? OR [4thPref]=? OR [5thPref]=?)",
                connection);
            cmd.Parameters.AddWithValue("?", lecturerId);
            for (int i = 0; i < 5; i++)
                cmd.Parameters.AddWithValue("?", subjectCode);

            return (int)cmd.ExecuteScalar() > 0;
        }

        /// <summary>
        /// Returns the LecturerID already teaching this subject in the
        /// Timetable (if any), or null if the subject is still unassigned.
        /// </summary>
        private string GetSubjectOwner(OleDbConnection connection, string subjectCode)
        {
            using var cmd = new OleDbCommand(
                "SELECT TOP 1 LecturerID FROM Timetable WHERE SubjectCode=?", connection);
            cmd.Parameters.AddWithValue("?", subjectCode);

            object result = cmd.ExecuteScalar();
            return result == null || result == DBNull.Value ? null : result.ToString();
        }

        /// <summary>
        /// Returns the LecturerID already occupying this exact (Day, SlotID)
        /// combination (if any), or null if the slot is still free. Used to
        /// enforce that only ONE class — one subject, one lecturer — can ever
        /// occupy a given day+time, regardless of subject or hall.
        /// </summary>
        private string GetSlotOwner(OleDbConnection connection, int slotId, string dayOfWeek)
        {
            using var cmd = new OleDbCommand(
                "SELECT TOP 1 LecturerID FROM Timetable WHERE SlotID=? AND DayOfWeek=?", connection);
            cmd.Parameters.AddWithValue("?", slotId);
            cmd.Parameters.AddWithValue("?", dayOfWeek);

            object result = cmd.ExecuteScalar();
            return result == null || result == DBNull.Value ? null : result.ToString();
        }

        private string GetLecturerName(string lecturerId)
        {
            using var connection = new OleDbConnection(_connectionString);
            connection.Open();
            using var cmd = new OleDbCommand("SELECT FullName FROM Lecturer WHERE LecturerID=?", connection);
            cmd.Parameters.AddWithValue("?", lecturerId);

            object result = cmd.ExecuteScalar();
            return result == null || result == DBNull.Value ? lecturerId : result.ToString();
        }

        private void InsertTimetableEntry(OleDbConnection connection, TimetableModel model)
        {
            using var insertCommand = new OleDbCommand(
                "INSERT INTO Timetable (LecturerID, SubjectCode, HallName, SlotID, DayOfWeek) VALUES (?, ?, ?, ?, ?)",
                connection);

            insertCommand.Parameters.AddWithValue("?", model.LecturerID);
            insertCommand.Parameters.AddWithValue("?", model.SubjectCode);
            insertCommand.Parameters.AddWithValue("?", model.HallName);
            insertCommand.Parameters.AddWithValue("?", model.SlotID);
            insertCommand.Parameters.AddWithValue("?", model.DayOfWeek);
            insertCommand.ExecuteNonQuery();
        }

        /// <summary>
        /// Builds the 9 selectable "Time Slot" dropdown options (positions
        /// 1-10, lunch excluded). Values are the 1-10 position, not the
        /// global SlotID, so every option's displayed text is unique.
        /// </summary>
        private List<DropdownItem> BuildSelectableTimeSlots()
        {
            return SlotData.GetAllSlots()
                .Take(SlotsPerDay)
                .Where(slot => !slot.Duration.Contains(LunchDuration))
                .Select(slot => new DropdownItem { Value = slot.SortOrder.ToString(), Text = slot.Duration })
                .ToList();
        }

        /// <summary>
        /// Groups timetable entries as [DayOfWeek][Duration] for the Weekly grid view.
        /// </summary>
        private Dictionary<string, Dictionary<string, TimetableModel>> BuildWeeklyGrid(List<TimetableModel> timetable)
        {
            var grid = WeekDays.ToDictionary(day => day, day => new Dictionary<string, TimetableModel>());

            foreach (TimetableModel entry in timetable)
            {
                if (grid.ContainsKey(entry.DayOfWeek))
                    grid[entry.DayOfWeek][entry.Duration] = entry;
            }

            return grid;
        }

        /// <summary>
        /// Builds LecturerID -> ordered list of that lecturer's preferred
        /// subjects (1st..5th, skipping unset ones), each with its rank
        /// label — used to populate the Subject dropdown client-side once a
        /// Lecturer is chosen.
        /// </summary>
        private Dictionary<string, List<Dictionary<string, string>>> BuildLecturerPreferenceMap()
        {
            var map = new Dictionary<string, List<Dictionary<string, string>>>();

            const string sql = @"SELECT lp.LecturerID,
                                 lp.[1stPref], s1.SubjectName AS N1,
                                 lp.[2ndPref], s2.SubjectName AS N2,
                                 lp.[3rdPref], s3.SubjectName AS N3,
                                 lp.[4thPref], s4.SubjectName AS N4,
                                 lp.[5thPref], s5.SubjectName AS N5
                          FROM (((((LecturerPreference lp
                          LEFT JOIN Subject s1 ON lp.[1stPref] = s1.SubjectCode)
                          LEFT JOIN Subject s2 ON lp.[2ndPref] = s2.SubjectCode)
                          LEFT JOIN Subject s3 ON lp.[3rdPref] = s3.SubjectCode)
                          LEFT JOIN Subject s4 ON lp.[4thPref] = s4.SubjectCode)
                          LEFT JOIN Subject s5 ON lp.[5thPref] = s5.SubjectCode)";

            using var connection = new OleDbConnection(_connectionString);
            using var adapter = new OleDbDataAdapter(sql, connection);

            var table = new DataTable();
            adapter.Fill(table);

            (string CodeCol, string NameCol, string Rank)[] ranks =
            {
                ("1stPref", "N1", "1st Choice"),
                ("2ndPref", "N2", "2nd Choice"),
                ("3rdPref", "N3", "3rd Choice"),
                ("4thPref", "N4", "4th Choice"),
                ("5thPref", "N5", "5th Choice"),
            };

            foreach (DataRow row in table.Rows)
            {
                string lecturerId = row["LecturerID"].ToString();
                var list = new List<Dictionary<string, string>>();

                foreach (var (codeCol, nameCol, rank) in ranks)
                {
                    if (row[codeCol] == DBNull.Value) continue;
                    string code = row[codeCol].ToString();
                    if (string.IsNullOrWhiteSpace(code)) continue;

                    list.Add(new Dictionary<string, string>
                    {
                        ["code"] = code,
                        ["name"] = row[nameCol] == DBNull.Value ? code : row[nameCol].ToString(),
                        ["rank"] = rank
                    });
                }

                map[lecturerId] = list;
            }

            return map;
        }

        /// <summary>
        /// Builds SubjectCode -> LecturerID for every subject that already
        /// has at least one Timetable entry — i.e. who currently "owns" it.
        /// </summary>
        private Dictionary<string, string> BuildAssignedSubjectsMap()
        {
            var map = new Dictionary<string, string>();

            using var connection = new OleDbConnection(_connectionString);
            using var adapter = new OleDbDataAdapter("SELECT DISTINCT SubjectCode, LecturerID FROM Timetable", connection);

            var table = new DataTable();
            adapter.Fill(table);

            foreach (DataRow row in table.Rows)
            {
                string code = row["SubjectCode"].ToString();
                if (!map.ContainsKey(code))
                    map[code] = row["LecturerID"].ToString();
            }

            return map;
        }

        private List<DropdownItem> LoadDropdownOptions(string sql, string valueField, string textField)
        {
            var options = new List<DropdownItem> { new DropdownItem { Value = "", Text = "-- Select --" } };

            using var connection = new OleDbConnection(_connectionString);
            using var adapter = new OleDbDataAdapter(sql, connection);

            var table = new DataTable();
            adapter.Fill(table);

            foreach (DataRow row in table.Rows)
            {
                options.Add(new DropdownItem
                {
                    Value = row[valueField].ToString(),
                    Text = row[textField].ToString()
                });
            }

            return options;
        }

        public List<TimetableModel> GetFullTimetable()
        {
            var results = new List<TimetableModel>();
            List<TimeSlotModel> allSlots = SlotData.GetAllSlots();

            const string sql = @"SELECT tt.TimetableID, tt.SlotID, tt.DayOfWeek, tt.HallName, tt.SubjectCode, tt.LecturerID,
                                         l.FullName AS LecturerName, s.SubjectName
                                  FROM ((Timetable tt
                                  INNER JOIN Lecturer l ON tt.LecturerID = l.LecturerID)
                                  INNER JOIN Subject s ON tt.SubjectCode = s.SubjectCode)";

            using (var connection = new OleDbConnection(_connectionString))
            using (var adapter = new OleDbDataAdapter(sql, connection))
            {
                var table = new DataTable();
                adapter.Fill(table);

                foreach (DataRow row in table.Rows)
                {
                    int slotId = Convert.ToInt32(row["SlotID"]);
                    TimeSlotModel slotInfo = allSlots.FirstOrDefault(s => s.SlotID == slotId);

                    results.Add(new TimetableModel
                    {
                        TimetableID = (int)row["TimetableID"],
                        SlotID = slotId,
                        DayOfWeek = row["DayOfWeek"].ToString(),
                        Duration = slotInfo?.Duration ?? "N/A",
                        SortOrder = slotInfo?.SortOrder ?? 0,
                        HallName = row["HallName"].ToString(),
                        SubjectCode = row["SubjectCode"].ToString(),
                        LecturerID = row["LecturerID"].ToString(),
                        LecturerName = row["LecturerName"].ToString(),
                        SubjectName = row["SubjectName"].ToString()
                    });
                }
            }

            return results;
        }
    }
}
