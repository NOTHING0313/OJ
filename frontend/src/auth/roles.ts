export function canManageContent(role?: number) {
  return role === 2 || role === 3;
}

export function isRoot(role?: number) {
  return role === 3;
}
