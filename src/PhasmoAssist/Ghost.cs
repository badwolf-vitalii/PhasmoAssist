using Enum;

namespace PhasmoAssist
{
    public class Ghost
    {
        public EGhost GhostType { get; set; }
        public List<EEvidence>? Evidences { get; set; }
        public bool Hidden { get; set; }

        public bool Check(Dictionary<EEvidence, bool> evidences)
        {
            if (evidences == null || !evidences.Any()) return false;
            if (Evidences == null || !Evidences.Any()) return false;
            foreach (var evidence in evidences.Where(p => p.Value).Select(p => p.Key))
            {
                if (!Evidences.Any(p => p == evidence))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
