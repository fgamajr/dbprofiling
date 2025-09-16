import { defineConfig } from "vite";
import react from "@vitejs/plugin-react-swc";
import path from "path";
import { componentTagger } from "lovable-tagger";

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => ({
  // Base path for production (relative paths for wwwroot integration)
  base: mode === "production" ? "./" : "/",
  
  // Build configuration for C# wwwroot integration
  build: {
    outDir: "dist",
    assetsDir: "assets",
    sourcemap: mode === "development",
    rollupOptions: {
      output: {
        manualChunks: {
          vendor: ['react', 'react-dom', 'react-router-dom'],
          ui: ['@radix-ui/react-toast', '@radix-ui/react-dialog', '@radix-ui/react-dropdown-menu']
        }
      }
    }
  },

  server: {
    host: "::",
    port: 8080,
    // Proxy API calls to C# backend during development
    proxy: mode === "development" ? {
      '/api': {
        target: 'http://localhost:5000', // C# dev server port
        changeOrigin: true,
        secure: false
      }
    } : undefined
  },
  
  plugins: [react(), mode === "development" && componentTagger()].filter(Boolean),
  
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
}));
