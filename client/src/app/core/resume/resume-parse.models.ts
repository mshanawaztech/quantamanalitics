export interface ParsedResumeWorkItem {
  employer: string;
  title: string;
  startDate: string | null;
  endDate: string | null;
}

export interface ParsedResumeResult {
  fullName: string | null;
  email: string | null;
  phoneNumber: string | null;
  headline: string | null;
  skills: string[];
  workHistory: ParsedResumeWorkItem[];
}
