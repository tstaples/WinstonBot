namespace WinstonBot.Services
{
    public class AoDDatabase
    {
        public enum Roles
        {
            Base,
            Chinner,
            Hammer,
            Umbra,
            Glacies,
            Cruor,
            Fumus
        }

        public static readonly Roles[] ExperiencedRoles = new[]
        {
                Roles.Base,
                Roles.Chinner,
                Roles.Hammer,
                Roles.Umbra,
                Roles.Glacies // ?
            };

        public enum ExperienceType
        {
            Experienced,
            Learner
        }

        public const int NumRoles = 7;

        // DB row: id, base weight, chin weight, ...
        // /configure aod add-role <user> <role> <weight? or default>
        // /configure aod set-role-weight <user> <role> <weight>
        // /configure aod remove-role <user> <role>
        // how do we set if they're learner/exp? Maybe just based on what role we add them to?
        // - eg adding someone to umbra would make them exp?
        public class User
        {
            public string Name { get; set; } // Temp
            public ulong Id { get; set; } = 0;
            // Range: 0-1
            public float[] RoleWeights { get; set; } = new float[NumRoles];
            public int SessionsAttendedInLastNDays { get; set; } = 0;

            public float GetRoleWeight(Roles role) => RoleWeights[(int)role];

            public User()
            {
                Array.Fill(RoleWeights, 0.0f);
            }

            // TODO: could cache this when loaded
            // TODO: might need to just explicitly set people to learner
            public ExperienceType GetExperience()
            {
                float fumusWeight = GetRoleWeight(Roles.Fumus);
                float cruorWeight = GetRoleWeight(Roles.Cruor);
                float learnerWeight = Math.Max(fumusWeight, cruorWeight);
                foreach (Roles expRole in ExperiencedRoles)
                {
                    float roleWeight = GetRoleWeight(expRole);
                    if (roleWeight > learnerWeight)
                    {
                        return ExperienceType.Experienced;
                    }
                }
                return ExperienceType.Learner;
            }
        }

        public List<User> UserEntries { get; set; } = new();
    }
}
