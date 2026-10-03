/** yyyy-MM-dd to dd/MM/yyyy, for messages. Anything else is returned as it is. */
export function formatDate(iso: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);

  return match ? `${match[3]}/${match[2]}/${match[1]}` : iso;
}
