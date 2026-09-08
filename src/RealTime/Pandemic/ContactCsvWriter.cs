namespace RealTime.Pandemic
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;

    /// <summary>Bounded reusable buffers for the numeric/enum-only contact CSV schema.</summary>
    internal sealed class ContactCsvWriter
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly string[] ContextNames = Enum.GetNames(typeof(PhysicalContactContext));
        private readonly StringBuilder row = new StringBuilder(512);
        private char[] buffer = new char[512];

        internal void Write(StreamWriter physical, StreamWriter traceable, PhysicalContactEvent contact,
            byte district, string startIso, string endIso)
        {
            row.Length = 0;
            row.Append(contact.ContactId.ToString(Invariant)).Append(',').Append(startIso).Append(',').Append(endIso).Append(',');
            Number(contact.DurationMinutes);
            row.Append(contact.CitizenA.ToString(Invariant)).Append(',').Append(contact.CitizenB.ToString(Invariant)).Append(',');
            int context = (int)contact.Context;
            row.Append(context >= 0 && context < ContextNames.Length ? ContextNames[context] : contact.Context.ToString()).Append(',');
            row.Append(contact.BuildingId.ToString(Invariant)).Append(',').Append(contact.VehicleId.ToString(Invariant)).Append(',');
            row.Append(district.ToString(Invariant)).Append(',');
            int commonLength = row.Length;
            Number(contact.PositionX); Number(contact.PositionY); Number(contact.PositionZ);
            if (contact.Distance.HasValue) row.Append(contact.Distance.Value.ToString("R", Invariant));
            row.Append(',');
            Flags(contact);
            Emit(physical);
            if (traceable != null && (contact.TraceableByApp || contact.TraceableByManual))
            {
                row.Length = commonLength;
                Flags(contact);
                Emit(traceable);
            }
        }

        private void Number(double value) => row.Append(value.ToString("R", Invariant)).Append(',');
        private void Flags(PhysicalContactEvent contact) => row.Append(contact.TraceableByApp ? '1' : '0').Append(',').Append(contact.TraceableByManual ? '1' : '0');

        private void Emit(StreamWriter writer)
        {
            using (PandemicProfiler.Measure("ScientificCsvRow"))
            {
                if (buffer.Length < row.Length) buffer = new char[row.Length];
                row.CopyTo(0, buffer, 0, row.Length);
                writer.Write(buffer, 0, row.Length);
                writer.WriteLine();
            }
        }
    }
}
