using System;
using System.IO;
using System.Windows.Forms;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace Drama_Queens_Patcher
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            string StoveDramaDataPath;
            string SteamDramaDataPath;

            using (CommonOpenFileDialog dialog = new CommonOpenFileDialog())
            {
                dialog.InitialDirectory = @"C:\Program Files (x86)\Smilegate\Games\동아리(시크릿 플러스)\DramaQueens_Data";
                dialog.IsFolderPicker = true;
                MessageBox.Show("Stove판 동아리(시크릿 플러스) 게임이 설치된 경로의 DramaQueens_Data 폴더를 선택해주세요", "DramaQueensPatcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
                    UserCancel();

                StoveDramaDataPath = dialog.FileName;

                dialog.InitialDirectory = @"C:\";
                MessageBox.Show("Steam판 동아리 게임의 resources.assets 파일과 resources.assets.resS 파일이 있는 폴더를 선택해주세요", "DramaQueensPatcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
                    UserCancel();

                SteamDramaDataPath = dialog.FileName;
            }

            File.WriteAllBytes(Path.GetTempPath() + "classdata.tpk", Properties.Resources.classdata);
            
            var manager = new AssetsManager();
            manager.LoadClassPackage(Path.GetTempPath() + "classdata.tpk");

            if (!File.Exists($@"{StoveDramaDataPath}\resources.assets") || 
                !File.Exists($@"{StoveDramaDataPath}\globalgamemanagers") || 
                !File.Exists($@"{SteamDramaDataPath}\resources.assets") ||
                !File.Exists($@"{SteamDramaDataPath}\resources.assets.resS"))
            {
                Console.WriteLine("필수 파일들이 확인되지 않았습니다");
                Console.WriteLine("다음 파일들이 모두 같은 폴더에 있는지 확인해주세요");
                Console.WriteLine("Stove : resources.assets, globalgamemanagers");
                Console.WriteLine("Steam : resources.assets, resources.assets.rssS");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("스토브 resources.assets 파일 이름 변경 : resources.assets.bak");
            File.Move($@"{StoveDramaDataPath}\resources.assets", $@"{StoveDramaDataPath}\resources.assets.bak");
            Console.WriteLine("스팀 CG 파일 복사중...");
            File.Copy($@"{SteamDramaDataPath}\resources.assets.resS", $@"{StoveDramaDataPath}\nxderesources.assets.resS");

            var originalAssets = manager.LoadAssetsFile($@"{StoveDramaDataPath}\resources.assets.bak", true);
            var nxdeAssets = manager.LoadAssetsFile($@"{SteamDramaDataPath}\resources.assets", true);
            var ggm = manager.LoadAssetsFile($@"{StoveDramaDataPath}\globalgamemanagers", false);

            manager.LoadClassDatabaseFromPackage(originalAssets.file.Metadata.UnityVersion);
            manager.LoadClassDatabaseFromPackage(nxdeAssets.file.Metadata.UnityVersion);
            manager.LoadClassDatabaseFromPackage(ggm.file.Metadata.UnityVersion);

            var rsrcInfo = ggm.file.GetAssetsOfType(AssetClassID.ResourceManager)[0];
            var rsrcBf = manager.GetBaseField(ggm, rsrcInfo);

            var m_Container = rsrcBf["m_Container.Array"];

            var NxdeTexturesInfo = nxdeAssets.file.GetAssetsOfType(AssetClassID.Texture2D);

            Console.WriteLine("스토브 CG 패치 시작");

            foreach (var TextureInfo in originalAssets.file.GetAssetsOfType(AssetClassID.Texture2D))
            {
                var TextureBase = manager.GetBaseField(originalAssets, TextureInfo);
                var data = m_Container.Children.Find(x => x[1]["m_PathID"].AsLong == TextureInfo.PathId);
                if (data == null)
                    continue;

                if (!data[0].AsString.Contains("art/ecg"))
                    continue;

                var NxdeTextureInfo = NxdeTexturesInfo.Find(x =>
                {
                    return manager.GetBaseField(nxdeAssets, x)["m_Name"].AsString == TextureBase["m_Name"].AsString;
                });
                if (NxdeTextureInfo == null)
                    continue;

                var NxdeTextureBase = manager.GetBaseField(nxdeAssets, NxdeTextureInfo);

                Console.WriteLine($"{TextureBase["m_Name"].AsString} stove offset : {TextureBase["m_StreamData.offset"].AsULong} steam offset : {NxdeTextureBase["m_StreamData.offset"].AsULong}");
                TextureBase["m_StreamData.offset"].AsULong = NxdeTextureBase["m_StreamData.offset"].AsULong;
                TextureBase["m_StreamData.path"].AsString = "nxderesources.assets.resS";

                TextureInfo.SetNewData(TextureBase);
            }

            Console.WriteLine("스토브 CG 패치 종료");

            using (AssetsFileWriter writer = new AssetsFileWriter($@"{StoveDramaDataPath}\resources.assets"))
            {
                originalAssets.file.Write(writer);
            }

            Console.WriteLine("스토브 CG 패치 저장");

            MessageBox.Show("스토브 CG 패치에 성공했습니다!\n패치가 정상적으로 적용되지 않은 경우는 일부 파일 또는 폴더 경로가 잘못되었을 수도 있습니다", "DramaQueensPatcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        static void UserCancel()
        {
            Console.WriteLine("사용자가 작업을 취소했습니다");
            Console.WriteLine("아무키나 입력하면 프로그램이 종료됩니다");
            Console.ReadKey();
            Application.ExitThread();
            Environment.Exit(0);
        }
    }
}
