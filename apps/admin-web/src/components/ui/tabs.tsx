'use client';

import * as React from 'react';
import { cn } from './button';

type TabsContextValue = {
  value?: string;
  onValueChange?: (value: string) => void;
};

const TabsContext = React.createContext<TabsContextValue>({});

interface TabsProps extends React.HTMLAttributes<HTMLDivElement> {
  value?: string;
  defaultValue?: string;
  onValueChange?: (value: string) => void;
}

function Tabs({ value, defaultValue, onValueChange, className, ...props }: TabsProps) {
  const [internalValue, setInternalValue] = React.useState(defaultValue);
  const selected = value ?? internalValue;
  const change = (next: string) => {
    if (value === undefined) setInternalValue(next);
    onValueChange?.(next);
  };
  return (
    <TabsContext.Provider value={{ value: selected, onValueChange: change }}>
      <div className={className} {...props} />
    </TabsContext.Provider>
  );
}

function TabsList({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('inline-flex h-10 items-center rounded-md bg-gray-100 p-1', className)} {...props} />;
}

interface TabsTriggerProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  value: string;
}

function TabsTrigger({ value, className, onClick, ...props }: TabsTriggerProps) {
  const tabs = React.useContext(TabsContext);
  const active = tabs.value === value;
  return (
    <button
      type="button"
      className={cn('rounded-sm px-3 py-1.5 text-sm', active && 'bg-white shadow-sm', className)}
      onClick={(event) => {
        onClick?.(event);
        tabs.onValueChange?.(value);
      }}
      {...props}
    />
  );
}

interface TabsContentProps extends React.HTMLAttributes<HTMLDivElement> {
  value: string;
}

function TabsContent({ value, className, ...props }: TabsContentProps) {
  const tabs = React.useContext(TabsContext);
  if (tabs.value !== value) return null;
  return <div className={cn('mt-2', className)} {...props} />;
}

export { Tabs, TabsContent, TabsList, TabsTrigger };
