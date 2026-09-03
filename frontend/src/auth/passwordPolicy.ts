const minimumPasswordLength = 15;
const maximumPasswordLength = 128;

export function getPasswordLengthError(password: string): string | null {
  const codePointLength = Array.from(password.normalize("NFC")).length;
  if (codePointLength < minimumPasswordLength) {
    return `密码至少需要 ${minimumPasswordLength} 个字符`;
  }

  if (codePointLength > maximumPasswordLength) {
    return `密码不能超过 ${maximumPasswordLength} 个字符`;
  }

  return null;
}
