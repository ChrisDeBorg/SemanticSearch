using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Minerva.Persistence.Entities
{
    public class Event : BaseEntity
    {
        public Event()
        {
            EntityType = "Event";
        }
        public DateTime? StartDate { get; set; } // Startdatum des Ereignisses
        public DateTime? EndDate { get; set; } // Enddatum des Ereignisses
        public string? Status { get; set; } // Status des Ereignisses (z.B. "Geplant", "Abgeschlossen")
        public string? ResponsibleEntity { get; set; } // Verantwortliche Organisation oder Person
        public List<string>? Stakeholders { get; set; } // Liste der beteiligten Interessengruppen
    }
}
