import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { HomePage } from "./HomePage";

function renderHomePage() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <HomePage />
    </QueryClientProvider>,
  );
}

describe("HomePage", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("çalışma bağlamını ve başarılı API durumunu erişilebilir biçimde gösterir", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ status: "ok" }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      ),
    );

    renderHomePage();

    expect(screen.getByRole("heading", { name: /güvenli erp çalışma alanı/i })).toBeVisible();
    expect(screen.getByRole("region", { name: /aktif çalışma bağlamı/i })).toBeVisible();
    expect(await screen.findByText("API erişilebilir")).toBeVisible();
  });

  it("API hatasını teknik ayrıntı sızdırmadan gösterir", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 503 })));

    renderHomePage();

    expect(await screen.findByText("API erişilemiyor")).toBeVisible();
  });
});

