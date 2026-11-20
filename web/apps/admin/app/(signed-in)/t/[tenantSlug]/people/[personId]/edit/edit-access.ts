// Who may edit a person's details (owner, S06-T7c-2b plan): the update replaces the whole biodata, the date of birth
// included, which only the private read gives, and a change to the names runs the duplicate check. Every role that holds
// `person.update` holds the other two today (S06 D21, Q42).
export const editDetailsActions = ["person.update", "person.read_private", "person.duplicate_check"] as const;
