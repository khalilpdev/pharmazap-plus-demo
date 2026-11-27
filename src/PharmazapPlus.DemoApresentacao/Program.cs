using Microsoft.Playwright;
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    public static async Task Main()
    {
        Console.WriteLine("🚀 Iniciando Motor de Demonstração Pharmazap Plus...");

        using var playwright = await Playwright.CreateAsync();

        // Headless = false para apresentação visual + SlowMo para acompanhar ações
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            SlowMo = 600
        });

        // Configurações (credenciais apenas via variáveis de ambiente)
        string baseUrl = Environment.GetEnvironmentVariable("DEMO_BASE_URL") ?? "https://localhost:7234";
        string outputDir = "Screenshots_Socio_Demo";
        string debugDir = Path.Combine(outputDir, "debug");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(debugDir);

        string? Env(string key) => Environment.GetEnvironmentVariable(key);

        var perfis = new[]
        {
            new { Nome = "GESTOR", Email = Env("DEMO_GESTOR_EMAIL"), Pass = Env("DEMO_GESTOR_PASSWORD"), Icon = "🏢" },
            new { Nome = "ADMIN", Email = Env("DEMO_ADMIN_EMAIL"), Pass = Env("DEMO_ADMIN_PASSWORD"), Icon = "👨‍💼" },
            new { Nome = "ATENDENTE", Email = Env("DEMO_ATENDENTE_EMAIL"), Pass = Env("DEMO_ATENDENTE_PASSWORD"), Icon = "💊" }
        }
        .Where(p => !string.IsNullOrWhiteSpace(p.Email) && !string.IsNullOrWhiteSpace(p.Pass))
        .Select(p => new { p.Nome, Email = p.Email!, Pass = p.Pass!, p.Icon })
        .ToArray();

        if (perfis.Length == 0)
        {
            Console.WriteLine("Nenhum perfil configurado. Defina DEMO_GESTOR_EMAIL/DEMO_GESTOR_PASSWORD, " +
                              "DEMO_ADMIN_EMAIL/DEMO_ADMIN_PASSWORD e DEMO_ATENDENTE_EMAIL/DEMO_ATENDENTE_PASSWORD.");
            return;
        }

        int index = 1;
        foreach (var perfil in perfis)
        {
            Console.WriteLine($"\n{perfil.Icon} Demonstrando Perfil: {perfil.Nome}...");

            // Cria um novo contexto limpo por usuário (sem cookies/localStorage herdados)
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1366, Height = 768 }
            });

            var page = await context.NewPageAsync();
            try
            {
                // 1) Navegar para a página de login e aguardar carregamento
                await page.GotoAsync($"{baseUrl}/login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
                await Task.Delay(500);

                // 2) Preencher os campos utilizando roles/labels (robusto para FluentUI)
                await page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).First.FillAsync(perfil.Email);
                await page.GetByRole(AriaRole.Textbox, new() { Name = "Senha" }).First.FillAsync(perfil.Pass);

                Console.WriteLine("   - Autenticando...");
                // 3) Clicar no botão principal de login
                await page.GetByRole(AriaRole.Button, new() { Name = "Entrar na Plataforma" }).First.ClickAsync();

                // 4) Aguardar redirecionamento /dashboard (tolerância para carregamentos do Blazor)
                await page.WaitForURLAsync("**/dashboard", new PageWaitForURLOptions { Timeout = 30000 });
                await Task.Delay(2000); // espera extra para garantir que gráficos & componentes renderizaram

                // 5) Tirar screenshot do dashboard
                string fileName = Path.Combine(outputDir, $"{index:00}_{perfil.Nome}_Dashboard.png");
                await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = fileName,
                    FullPage = false
                });
                Console.WriteLine($"   ✅ Screenshot salvo: {fileName}");

                // 6) Se for ADMIN, abrir o cadastro de atendente, preencher e salvar + screenshot extra
                if (perfil.Nome == "ADMIN")
                {
                    Console.WriteLine("   🔧 Acessando página de Atendentes para capturar formulário de cadastro...");

                    await page.GotoAsync($"{baseUrl}/atendentes", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
                    await Task.Delay(800);

                    bool openedForm = false;

                    // Tentativa robusta para abrir o formulário
                    try
                    {
                        var btns = page.Locator("button:has-text('Cadastrar Novo Atendente'), button:has-text('Cadastrar'), button:has-text('Novo Atendente'), button:has-text('Cadastrar Atendente')");
                        if (await btns.First.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 4000 }))
                        {
                            await btns.First.ClickAsync();
                            openedForm = true;
                        }
                    }
                    catch { /* ignore */ }

                    if (!openedForm)
                    {
                        try
                        {
                            await page.EvaluateAsync(@"() => {
                                const btn = Array.from(document.querySelectorAll('button')).find(b => /cadastrar/i.test(b.innerText) || /novo atendente/i.test(b.innerText));
                                if (btn) btn.click();
                            }");
                            await Task.Delay(900);
                            openedForm = true;
                        }
                        catch { /* ignore */ }
                    }

                    // Aguarda diálogo/ formulário aparecer (FluentDialog / modal)
                    try
                    {
                        // Usamos WaitForAsync no locator do diálogo com o estado "attached" por padrão
                        var dialogLocator = page.Locator("[role='dialog']");
                        if (await dialogLocator.CountAsync() > 0)
                        {
                            await dialogLocator.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 8000, State = WaitForSelectorState.Attached });
                        }
                    }
                    catch
                    {
                        Console.WriteLine("   ⚠️ Diálogo de novo atendente não apareceu dentro do timeout (continuando com tentativa de preenchimento inline).");
                    }

                    // --- Robusto: preencher e submeter o formulário com várias estratégias ---
                    // Preenche o formulário de criação de atendente (cria email único para evitar conflito)
                    string uniqueEmail = $"e2e.atendente.{DateTime.UtcNow:yyyyMMddHHmmss}@test.local";

                    try
                    {
                        // Localizadores principais (tenta por label/role)
                        var nomeLocator = page.GetByRole(AriaRole.Textbox, new() { Name = "Nome" }).First;
                        var emailLocator = page.GetByRole(AriaRole.Textbox, new() { Name = "Email" }).First;
                        var telLocator = page.GetByRole(AriaRole.Textbox, new() { Name = "Telefone" }).First;
                        var senhaLocator = page.GetByRole(AriaRole.Textbox, new() { Name = "Senha" }).First;

                        // Preenche se visíveis (timeouts curtos por campo)
                        if (await nomeLocator.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 2500 }))
                            await nomeLocator.FillAsync("Atendente E2E Teste");

                        if (await emailLocator.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 2500 }))
                            await emailLocator.FillAsync(uniqueEmail);

                        if (await telLocator.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 1200 }))
                            await telLocator.FillAsync("(11) 99999-0000");

                        if (await senhaLocator.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 1200 }))
                            await senhaLocator.FillAsync("SenhaE2E123");

                        // Estratégia 1: botão dentro do diálogo (role=dialog) ou na página - procura vários textos
                        var dialog = page.Locator("[role='dialog']");
                        bool clicked = false;

                        try
                        {
                            if (await dialog.CountAsync() > 0)
                            {
                                var submitBtn = dialog.First.Locator("button:has-text('Criar'), button:has-text('Salvar'), fluent-button:has-text('Criar'), fluent-button:has-text('Salvar')");
                                if (await submitBtn.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 2500 }))
                                {
                                    await submitBtn.ClickAsync();
                                    clicked = true;
                                }
                            }
                        }
                        catch { /* ignore */ }

                        // Estratégia 2: botão em qualquer lugar da página com os textos esperados
                        if (!clicked)
                        {
                            var anyBtn = page.Locator("button:has-text('Criar'), button:has-text('Salvar'), fluent-button:has-text('Criar'), fluent-button:has-text('Salvar')");
                            try
                            {
                                if (await anyBtn.First.IsVisibleAsync(new LocatorIsVisibleOptions { Timeout = 2500 }))
                                {
                                    await anyBtn.First.ClickAsync();
                                    clicked = true;
                                }
                            }
                            catch { /* ignore */ }
                        }

                        // Estratégia 3: pressionar Enter no último campo preenchido (fallback de submissão)
                        if (!clicked)
                        {
                            try
                            {
                                await senhaLocator.PressAsync("Enter");
                                clicked = true;
                            }
                            catch { /* ignore */ }
                        }

                        // Estratégia 4: fallback DOM regex click
                        if (!clicked)
                        {
                            try
                            {
                                await page.EvaluateAsync(@"() => {
                                    const candidates = Array.from(document.querySelectorAll('button, fluent-button'));
                                    const regex = /^(Criar|Salvar)/i;
                                    const btn = candidates.find(b => regex.test((b.innerText || b.textContent || '').trim()));
                                    if (btn) btn.click();
                                }");
                                clicked = true;
                            }
                            catch { /* ignore */ }
                        }

                        // Aguarda confirmação (toast) ou fechamento do dialog
                        try
                        {
                            await page.GetByText("Atendente criado com sucesso!").WaitForAsync(new LocatorWaitForOptions { Timeout = 8000 });
                        }
                        catch
                        {
                            try
                            {
                                var dialogLocator = page.Locator("[role='dialog']");
                                if (await dialogLocator.CountAsync() > 0)
                                {
                                    // Espera o diálogo ser removido (detached) usando WaitForAsync com State = Detached
                                    await dialogLocator.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 6000, State = WaitForSelectorState.Detached });
                                }
                                else
                                {
                                    await Task.Delay(1200);
                                }
                            }
                            catch { /* ignore */ }
                        }
                    }
                    catch (Exception exForm)
                    {
                        // Em caso de erro, salva dump do dialog HTML para diagnóstico
                        Console.WriteLine($"   ⚠️ Erro ao preencher/confirmar formulário do atendente (fallbacks aplicadas): {exForm.Message}");
                        try
                        {
                            var dialogCount = await page.Locator("[role='dialog']").CountAsync();
                            if (dialogCount > 0)
                            {
                                var dialogElem = page.Locator("[role='dialog']").First;
                                var dialogHtml = await dialogElem.InnerHTMLAsync();
                                var debugFile = Path.Combine(debugDir, $"admin_dialog_html_{DateTime.UtcNow:yyyyMMddHHmmss}.html");
                                await File.WriteAllTextAsync(debugFile, dialogHtml);
                                Console.WriteLine($"   🐞 Debug: HTML do diálogo salvo em: {debugFile}");
                            }
                            else
                            {
                                var fullHtml = await page.ContentAsync();
                                var debugFileFull = Path.Combine(debugDir, $"admin_page_full_html_{DateTime.UtcNow:yyyyMMddHHmmss}.html");
                                await File.WriteAllTextAsync(debugFileFull, fullHtml);
                                Console.WriteLine($"   🐞 Debug: HTML da página salvo em: {debugFileFull}");
                            }
                        }
                        catch (Exception dbgEx)
                        {
                            Console.WriteLine($"   ⚠️ Falha ao salvar debug HTML: {dbgEx.Message}");
                        }
                    }

                    // Espera extra para renderizar resultado e tira screenshot final do formulário/resultado
                    await Task.Delay(1200);
                    string adminFormFile = Path.Combine(outputDir, $"{index:00}_{perfil.Nome}_AtendenteForm_Filled.png");
                    await page.ScreenshotAsync(new PageScreenshotOptions
                    {
                        Path = adminFormFile,
                        FullPage = false
                    });
                    Console.WriteLine($"   ✅ Screenshot do cadastro de atendente salvo: {adminFormFile}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Erro ao processar perfil {perfil.Nome}: {ex.Message}");
                // adicional: salvar screenshot de erro
                try
                {
                    var errorFile = Path.Combine(debugDir, $"{index:00}_{perfil.Nome}_error_{DateTime.UtcNow:yyyyMMddHHmmss}.png");
                    await page.ScreenshotAsync(new PageScreenshotOptions { Path = errorFile, FullPage = false });
                    Console.WriteLine($"   🐞 Screenshot de erro salvo: {errorFile}");
                }
                catch { /* ignore */ }
            }
            finally
            {
                // Fecha o contexto, removendo qualquer estado antes do próximo perfil
                await context.CloseAsync();
                index++;
            }
        }

        await browser.CloseAsync();
        Console.WriteLine("\n🏁 Demonstração Finalizada! Os prints estão na pasta 'Screenshots_Socio_Demo'.");
        Console.WriteLine("Debug HTML (se gerado) está em 'Screenshots_Socio_Demo/debug'.");
        Console.WriteLine("Pressione qualquer tecla para sair...");
        Console.ReadKey();
    }
}