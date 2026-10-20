// Platform reference data for pickers (`/api/public/v1/reference/*`): cacheable for an hour, so read once per visit.
import { useQuery } from "@tanstack/react-query";
import { api, call } from "./api";

const staleTime = 60 * 60 * 1000;

export function useTimeZones() {
  return useQuery({
    queryKey: ["reference", "time-zones"],
    queryFn: () => call(api.GET("/api/public/v1/reference/time-zones")),
    staleTime,
  });
}

export function useCurrencies() {
  return useQuery({
    queryKey: ["reference", "currencies"],
    queryFn: () => call(api.GET("/api/public/v1/reference/currencies")),
    staleTime,
  });
}

export function useCountries() {
  return useQuery({
    queryKey: ["reference", "countries"],
    queryFn: () => call(api.GET("/api/public/v1/reference/countries")),
    staleTime,
  });
}
