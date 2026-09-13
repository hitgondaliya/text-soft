import { parsePhoneNumberFromString, type CountryCode } from "libphonenumber-js";
import { findCountry } from "./countries";

export interface ParsedPhone {
  country: string; // ISO alpha-2
  national: string; // digits as the user would dial locally
  e164: string; // "+919825000000"
}

/**
 * Validate a national number against its country with libphonenumber, so
 * "98250 00000" for IN passes and "1234" does not. Returns null when invalid.
 * The same library (its C# port) runs in the WPF app, so both sides agree.
 */
export function parsePhone(country: string, national: string): ParsedPhone | null {
  const c = findCountry(country);
  if (!c) return null;
  const digits = national.replace(/[^\d]/g, "");
  if (digits.length < 4 || digits.length > 15) return null;
  const parsed = parsePhoneNumberFromString(digits, c.code as CountryCode);
  if (!parsed || !parsed.isValid()) return null;
  return { country: c.code, national: parsed.nationalNumber, e164: parsed.number };
}

/** "+91 98250 00000" for display. */
export function formatPhone(e164: string | undefined | null): string {
  if (!e164) return "";
  const parsed = parsePhoneNumberFromString(e164);
  return parsed ? parsed.formatInternational() : e164;
}
