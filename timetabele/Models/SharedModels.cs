using System.Collections.Generic;

namespace TimetableApp.Models
{
    // DropdownItem පන්තිය
    public class DropdownItem
    {
        public string Value { get; set; }
        public string Text { get; set; }
    }

    // ErrorViewModel පන්තිය
    public class ErrorViewModel
    {
        public string RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }

    // අලුතින් එක් කළ SlotData පන්තිය
    public static class SlotData
    {
        public static List<TimeSlotModel> GetAllSlots()
        {
            var slots = new List<TimeSlotModel>();

            // EXACT times from the official printed timetable.
            // Index 4 (12.30-1.00) is the LUNCH row — never assigned a
            // real class, only used as a placeholder so the grid keeps
            // its fixed 10-row shape.
            string[] timeRanges = {
                "8.30-9.30", "9.30-10.30", "10.30-11.30", "11.30-12.30",
                "12.30-1.00", "1.00-2.00", "2.00-3.00", "3.00-4.00",
                "4.00-5.00", "5.00-6.00"
            };

            string[] days = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

            int slotId = 1;

            foreach (var day in days)
            {
                for (int i = 0; i < timeRanges.Length; i++)
                {
                    slots.Add(new TimeSlotModel
                    {
                        SlotID = slotId,
                        Duration = timeRanges[i],   // TIME ONLY — no day prefix.
                        SortOrder = slotId
                    });
                    slotId++;
                }
            }

            return slots;
        }
    }
}