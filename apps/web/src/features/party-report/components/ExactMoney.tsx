import type { ReportAmount } from "../api/partyAccountReport";

interface ExactMoneyProps {
  amount: ReportAmount;
  currency: string;
}

export function ExactMoney({ amount, currency }: ExactMoneyProps) {
  if (amount.visibility === "redacted") {
    return <span className="money-redacted">Yetkiniz kapsamında gizli</span>;
  }

  return (
    <span className="money-value">
      {formatDecimalString(amount.value)} <span className="money-currency">{currency}</span>
    </span>
  );
}

function formatDecimalString(value: string): string {
  const negative = value.startsWith("-");
  const unsigned = negative ? value.slice(1) : value;
  const [whole, fraction] = unsigned.split(".");

  if (whole === undefined) {
    throw new Error("Validated decimal string has no whole-number component.");
  }

  const groupedWhole = whole.replace(/\B(?=(\d{3})+(?!\d))/gu, ".");
  const localized = fraction === undefined ? groupedWhole : `${groupedWhole},${fraction}`;
  return negative ? `-${localized}` : localized;
}
