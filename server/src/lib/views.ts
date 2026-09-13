import { formatPhone } from "./phone";
import type { ActivationDoc, ActivationAttemptDoc, AdminDoc, EndUser } from "./models";

// JSON shapes the admin pages consume. One place, so the key page, the purchase
// page and the lists all describe a device or a person the same way.

export function userView(u: EndUser | undefined | null) {
  if (!u) return null;
  return {
    name: u.name,
    email: u.email,
    phoneCountry: u.phoneCountry,
    phone: u.phone,
    phoneE164: u.phoneE164,
    phoneDisplay: formatPhone(u.phoneE164),
    registeredUtc: u.registeredUtc.toISOString(),
  };
}

export function activationView(a: ActivationDoc) {
  return {
    id: a._id!.toHexString(),
    keyId: a.keyId.toHexString(),
    serial: a.serial ?? "",
    deviceFp: a.deviceFp,
    deviceName: a.deviceName ?? "",
    ip: a.ip ?? "",
    appVersion: a.appVersion ?? "",
    status: a.status,
    activatedUtc: a.activatedUtc.toISOString(),
    lastSeenUtc: a.lastSeenUtc.toISOString(),
    user: userView(a.user),
    releasedUtc: a.releasedUtc ? a.releasedUtc.toISOString() : null,
    releasedBy: a.releasedBy ?? "",
    releaseReason: a.releaseReason ?? "",
  };
}

export function attemptView(t: ActivationAttemptDoc) {
  return {
    id: t._id!.toHexString(),
    keyId: t.keyId ? t.keyId.toHexString() : null,
    serial: t.serial ?? "",
    seq: t.seq ?? null,
    keyPrefix: t.keyPrefix ?? t.typedPrefix ?? "",
    known: !!t.keyId,
    deviceName: t.deviceName ?? "",
    deviceFp: t.deviceFp ?? "",
    ip: t.ip,
    appVersion: t.appVersion ?? "",
    reason: t.reason,
    source: t.source,
    boundDeviceName: t.boundDeviceName ?? "",
    createdUtc: t.createdUtc.toISOString(),
  };
}

export function adminView(a: AdminDoc) {
  return {
    id: a._id!.toHexString(),
    email: a.email,
    name: a.name ?? "",
    role: a.role,
    status: a.status ?? "active",
    createdUtc: a.createdUtc.toISOString(),
    createdBy: a.createdBy ?? "",
    lastLoginUtc: a.lastLoginUtc ? a.lastLoginUtc.toISOString() : null,
  };
}

